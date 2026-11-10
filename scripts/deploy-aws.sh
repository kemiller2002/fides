#!/usr/bin/env bash
# Sets up and updates the Fides exchange in the operator's own AWS account
# (docs/hosting/AWS.md, DF-FIDES-2026-0005). The operator runs it; nothing in
# this repository runs it. It never prints, stores or passes the GitHub client
# secret on a command line.
#
#   scripts/deploy-aws.sh init            set up an environment (run once)
#   scripts/deploy-aws.sh update VERSION  move an environment to a release
#   scripts/deploy-aws.sh check           check a deployed environment
#
# Needs: aws (CLI v2, logged in), gh (logged in), git, curl, and sha256sum or
# shasum. Non-secret settings are kept in ~/.fides-deploy/<environment>.env.
set -euo pipefail

readonly REPOSITORY="kemiller2002/fides"
readonly PACKAGE="fides-exchange-linux-arm64.zip"
readonly STATE_DIR="${FIDES_DEPLOY_HOME:-$HOME/.fides-deploy}"
readonly ENVIRONMENT="${FIDES_ENVIRONMENT:-production}"
readonly SETTINGS="$STATE_DIR/$ENVIRONMENT.env"
readonly WORK_DIR="$STATE_DIR/$ENVIRONMENT"

# ---- defaults for echelonfoundry.com -------------------------------------
readonly DEFAULT_ZONE="echelonfoundry.com"
readonly DEFAULT_DOMAIN="auth.echelonfoundry.com"
readonly DEFAULT_REGION="us-east-2"
readonly DEFAULT_VERSION="0.2.0"

say()  { printf '\n\033[1m==> %s\033[0m\n' "$*"; }
note() { printf '    %s\n' "$*"; }
fail() { printf '\n\033[31mstopped: %s\033[0m\n' "$*" >&2; exit 1; }

ask() { # ask VARIABLE "question" [default]
  local answer
  read -rp "    $2${3:+ [$3]}: " answer
  printf -v "$1" '%s' "${answer:-${3:-}}"
  [[ -n "${!1}" ]] || fail "$2 is required"
}

confirm() { local answer; read -rp "    $1 [y/N]: " answer; [[ "$answer" =~ ^[Yy] ]]; }

require_tools() {
  local tool
  for tool in aws gh git curl; do command -v "$tool" >/dev/null || fail "$tool is not installed"; done
  command -v sha256sum >/dev/null || command -v shasum >/dev/null || fail "sha256sum or shasum is needed"
  aws sts get-caller-identity --query Account --output text >/dev/null 2>&1 || fail "the AWS CLI is not logged in (run: aws configure, or aws sso login)"
  gh auth status >/dev/null 2>&1 || fail "the GitHub CLI is not logged in (run: gh auth login)"
}

checksum() { if command -v sha256sum >/dev/null; then sha256sum -c --ignore-missing checksums.txt; else shasum -a 256 -c --ignore-missing checksums.txt; fi; }

save_setting() { # save_setting NAME VALUE
  mkdir -p "$STATE_DIR"; touch "$SETTINGS"
  grep -v "^$1=" "$SETTINGS" > "$SETTINGS.tmp" || true
  printf '%s=%q\n' "$1" "$2" >> "$SETTINGS.tmp"
  mv "$SETTINGS.tmp" "$SETTINGS"
}

load_settings() {
  [[ -f "$SETTINGS" ]] || fail "no settings for '$ENVIRONMENT'; run: $0 init"
  # shellcheck source=/dev/null
  source "$SETTINGS"
}

# ---- DNS --------------------------------------------------------------------
hosted_zone() { # the Route 53 zone id for ZONE, or nothing
  aws route53 list-hosted-zones-by-name --dns-name "$ZONE" \
    --query "HostedZones[?Name=='$ZONE.' && Config.PrivateZone==\`false\`].Id | [0]" --output text 2>/dev/null \
    | sed 's#/hostedzone/##; s#^None$##'
}

upsert_cname() { # upsert_cname NAME VALUE
  local zone_id; zone_id="$(hosted_zone)"
  if [[ -n "$zone_id" ]]; then
    aws route53 change-resource-record-sets --hosted-zone-id "$zone_id" --change-batch \
      "{\"Changes\":[{\"Action\":\"UPSERT\",\"ResourceRecordSet\":{\"Name\":\"$1\",\"Type\":\"CNAME\",\"TTL\":300,\"ResourceRecords\":[{\"Value\":\"$2\"}]}}]}" \
      >/dev/null
    note "Route 53: CNAME $1 -> $2"
  else
    note "Add this record at your DNS provider, then press Enter:"
    note "  type CNAME   name $1   value $2"
    read -r
  fi
}

# ---- steps ------------------------------------------------------------------
collect_settings() {
  say "Settings for the '$ENVIRONMENT' environment"
  ask REGION "AWS region" "${REGION:-$DEFAULT_REGION}"
  ask ZONE "Your domain" "${ZONE:-$DEFAULT_ZONE}"
  ask DOMAIN "Host name for the Fides exchange" "${DOMAIN:-$DEFAULT_DOMAIN}"
  [[ "$DOMAIN" =~ ^[a-z0-9.-]+$ ]] || fail "the host name must be lowercase"
  ask APP_ORIGIN "Your application's origin (https://host, no trailing slash)" "${APP_ORIGIN:-}"
  ask APP_CALLBACK "Your application's sign-in callback URL" "${APP_CALLBACK:-$APP_ORIGIN/auth/callback}"
  ask DATA_REPO "The application's data repository (owner/name)" "${DATA_REPO:-}"
  ask APP_ID "Application id (a-z, 0-9, -)" "${APP_ID:-$(basename "$APP_ORIGIN" | cut -d. -f1)-$ENVIRONMENT}"
  [[ "$APP_ID" =~ ^[a-z0-9-]{1,64}$ ]] || fail "the application id must be 1-64 characters of a-z, 0-9 and -"
  ACCOUNT="$(aws sts get-caller-identity --query Account --output text)"
  BUCKET="${BUCKET:-fides-artifacts-$ACCOUNT-$REGION}"
  local name; for name in REGION ZONE DOMAIN APP_ORIGIN APP_CALLBACK DATA_REPO APP_ID BUCKET; do save_setting "$name" "${!name}"; done
}

github_app() {
  say "Register the GitHub App (in your browser)"
  cat <<EOF
    Open: https://github.com/settings/apps/new
      (for an organization: https://github.com/organizations/ORG/settings/apps/new)

      GitHub App name ................ Fides ($ENVIRONMENT)
      Homepage URL ................... $APP_ORIGIN
      Callback URL ................... $APP_CALLBACK
      Expire user authorization tokens  CHECKED
      Request user authorization (OAuth) during installation  unchecked
      Enable Device Flow ............. unchecked
      Webhook > Active ............... UNCHECKED
      Repository permissions ......... Contents: Read and write; Metadata: Read-only
      Organization/account permissions  none
      Where can this App be installed  Only on this account

    Then: copy the Client ID; click "Generate a new client secret" and keep
    that page open; click "Install App" and choose only $DATA_REPO.
    Do not generate a private key.
EOF
  ask CLIENT_ID "Client ID (starts with Iv)" "${CLIENT_ID:-}"
  [[ "$CLIENT_ID" =~ ^Iv[0-9A-Za-z._-]+$ ]] || fail "that is not a GitHub App client id"
  save_setting CLIENT_ID "$CLIENT_ID"
}

certificate() {
  say "TLS certificate for $DOMAIN ($REGION)"
  CERT_ARN="$(aws acm list-certificates --region "$REGION" --certificate-statuses ISSUED PENDING_VALIDATION \
    --query "CertificateSummaryList[?DomainName=='$DOMAIN'].CertificateArn | [0]" --output text)"
  if [[ "$CERT_ARN" == "None" || -z "$CERT_ARN" ]]; then
    CERT_ARN="$(aws acm request-certificate --region "$REGION" --domain-name "$DOMAIN" --validation-method DNS --query CertificateArn --output text)"
    note "requested $CERT_ARN"
  else
    note "reusing $CERT_ARN"
  fi
  save_setting CERT_ARN "$CERT_ARN"
  local status; status="$(aws acm describe-certificate --region "$REGION" --certificate-arn "$CERT_ARN" --query Certificate.Status --output text)"
  if [[ "$status" != "ISSUED" ]]; then
    local name="None" value
    for _ in $(seq 1 20); do
      name="$(aws acm describe-certificate --region "$REGION" --certificate-arn "$CERT_ARN" --query 'Certificate.DomainValidationOptions[0].ResourceRecord.Name' --output text)"
      [[ "$name" != "None" ]] && break; sleep 3
    done
    value="$(aws acm describe-certificate --region "$REGION" --certificate-arn "$CERT_ARN" --query 'Certificate.DomainValidationOptions[0].ResourceRecord.Value' --output text)"
    upsert_cname "$name" "$value"
    note "waiting for the certificate to be issued (usually a few minutes)..."
    aws acm wait certificate-validated --region "$REGION" --certificate-arn "$CERT_ARN"
  fi
  note "certificate issued"
}

client_secret() {
  say "GitHub App client secret -> AWS Secrets Manager"
  local name="fides/$ENVIRONMENT/github-client-secret" existing secret file
  existing="$(aws secretsmanager describe-secret --region "$REGION" --secret-id "$name" --query ARN --output text 2>/dev/null || true)"
  if [[ -n "$existing" ]] && ! confirm "A secret already exists for $ENVIRONMENT. Replace its value?"; then
    SECRET_ARN="$existing"
  else
    read -rsp "    Paste the client secret (nothing is shown), then Enter: " secret; echo
    [[ -n "$secret" ]] || fail "no secret was pasted"
    file="$(umask 077; mktemp)"
    printf '%s' "$secret" > "$file"; unset secret
    if [[ -n "$existing" ]]; then
      aws secretsmanager put-secret-value --region "$REGION" --secret-id "$name" --secret-string "file://$file" >/dev/null
      SECRET_ARN="$existing"
    else
      SECRET_ARN="$(aws secretsmanager create-secret --region "$REGION" --name "$name" --secret-string "file://$file" --query ARN --output text)"
    fi
    rm -f "$file"
    note "stored. You can close the GitHub page; the secret is not shown again."
  fi
  save_setting SECRET_ARN "$SECRET_ARN"
}

release() { # release VERSION: download, verify, upload; leaves the template in $WORK_DIR/src
  say "Release v$1: download, verify, upload"
  mkdir -p "$WORK_DIR"; cd "$WORK_DIR"
  rm -rf src "$PACKAGE" checksums.txt
  gh release download "v$1" --repo "$REPOSITORY" --pattern "$PACKAGE" --pattern checksums.txt
  checksum || fail "checksum mismatch"
  gh attestation verify "$PACKAGE" --repo "$REPOSITORY" >/dev/null || fail "attestation did not verify"
  note "checksum and Sigstore attestation verified"
  git clone --quiet --depth 1 --branch "v$1" "https://github.com/$REPOSITORY" src
  aws s3api head-bucket --bucket "$BUCKET" 2>/dev/null \
    || aws s3 mb "s3://$BUCKET" --region "$REGION" >/dev/null
  aws s3 cp --quiet "$PACKAGE" "s3://$BUCKET/fides/$1/$PACKAGE"
  note "uploaded s3://$BUCKET/fides/$1/$PACKAGE"
}

applications_json() {
  printf '[{"id":"%s","provider":"github","origins":["%s"],"redirectUris":["%s"],"requiredRepository":"%s"}]' \
    "$APP_ID" "$APP_ORIGIN" "$APP_CALLBACK" "$DATA_REPO"
}

stack() { printf 'fides-exchange-%s' "$ENVIRONMENT"; }

deploy_stack() { # deploy_stack VERSION
  say "Deploying stack $(stack)"
  aws cloudformation deploy --region "$REGION" --stack-name "$(stack)" \
    --template-file "$WORK_DIR/src/infrastructure/aws/fides-exchange.template.json" \
    --capabilities CAPABILITY_IAM --no-fail-on-empty-changeset \
    --parameter-overrides EnvironmentName="$ENVIRONMENT" DomainName="$DOMAIN" CertificateArn="$CERT_ARN" \
      GitHubClientId="$CLIENT_ID" ClientSecretArn="$SECRET_ARN" ApplicationsJson="$(applications_json)" \
      CodeBucket="$BUCKET" CodeKey="fides/$1/$PACKAGE"
  save_setting VERSION "$1"
}

point_dns() {
  say "DNS for $DOMAIN"
  local target
  target="$(aws cloudformation describe-stacks --region "$REGION" --stack-name "$(stack)" \
    --query "Stacks[0].Outputs[?OutputKey=='RegionalDomainName'].OutputValue" --output text)"
  upsert_cname "$DOMAIN" "$target"
}

check() {
  say "Checking https://$DOMAIN"
  local good bad
  for _ in $(seq 1 30); do
    good="$(curl -s -o /dev/null -w '%{http_code}' -X OPTIONS "https://$DOMAIN/v1/token" -H "Origin: $APP_ORIGIN" || true)"
    [[ "$good" == "204" ]] && break; sleep 10
  done
  bad="$(curl -s -o /dev/null -w '%{http_code}' -X OPTIONS "https://$DOMAIN/v1/token" -H "Origin: https://not-registered.example" || true)"
  note "registered origin -> $good (expect 204)"
  note "unknown origin    -> $bad (expect 403)"
  [[ "$good" == "204" && "$bad" == "403" ]] || fail "the exchange did not answer as expected; see the function's CloudWatch log group"
  cat <<EOF

    Fides is live. Configure your application's Fides client with:
      exchange URL   https://$DOMAIN
      application    $APP_ID
      client id      $CLIENT_ID
      redirect URI   $APP_CALLBACK
      retention      memory-only (the default)
    Then sign in once from the application.
EOF
}

# ---- commands ---------------------------------------------------------------
init() {
  require_tools
  # shellcheck source=/dev/null
  [[ -f "$SETTINGS" ]] && source "$SETTINGS"
  collect_settings
  github_app
  certificate
  client_secret
  release "${VERSION:-$DEFAULT_VERSION}"
  deploy_stack "${VERSION:-$DEFAULT_VERSION}"
  point_dns
  check
}

update() { # update VERSION
  [[ -n "${1:-}" ]] || fail "usage: $0 update VERSION (for example 0.3.0)"
  require_tools; load_settings
  local previous="${VERSION:-unknown}"
  release "$1"
  say "Preview: $(stack) from v$previous to v$1"
  local output change_set
  output="$(aws cloudformation deploy --region "$REGION" --stack-name "$(stack)" \
    --template-file "$WORK_DIR/src/infrastructure/aws/fides-exchange.template.json" \
    --capabilities CAPABILITY_IAM --no-execute-changeset \
    --parameter-overrides CodeKey="fides/$1/$PACKAGE")"
  change_set="$(grep -o 'arn:aws[a-z-]*:cloudformation:[^ ]*changeSet[^ ]*' <<<"$output" | head -1)"
  [[ -n "$change_set" ]] || { note "$output"; fail "no change set was created"; }
  aws cloudformation describe-change-set --region "$REGION" --change-set-name "$change_set" \
    --query '{Parameters: Parameters[].[ParameterKey, ParameterValue], Changes: Changes[].ResourceChange.[Action, LogicalResourceId]}' --output table
  if confirm "Apply this change?"; then
    aws cloudformation execute-changeset --region "$REGION" --change-set-name "$change_set"
    aws cloudformation wait stack-update-complete --region "$REGION" --stack-name "$(stack)"
    save_setting VERSION "$1"
    check
    note "to roll back: $0 update $previous"
  else
    aws cloudformation delete-change-set --region "$REGION" --change-set-name "$change_set"
    note "nothing changed"
  fi
}

case "${1:-}" in
  init) init ;;
  update) update "${2:-}" ;;
  check) require_tools; load_settings; check ;;
  *) sed -n '2,12p' "$0" | sed 's/^# \{0,1\}//'; exit 2 ;;
esac
