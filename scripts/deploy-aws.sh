#!/usr/bin/env bash
# Sets up and updates the Fides exchange in the operator's own AWS account
# (docs/hosting/AWS.md, DF-FIDES-2026-0005, DF-FIDES-2026-0016). The operator
# runs it; nothing in this repository runs it. It never prints, stores or
# passes the GitHub client secret on a command line.
#
#   scripts/deploy-aws.sh init            set up an environment (run once; safe to re-run)
#   scripts/deploy-aws.sh add-app         register another Echelon application
#   scripts/deploy-aws.sh apps            list the registered applications
#   scripts/deploy-aws.sh update VERSION  move the environment to a release
#   scripts/deploy-aws.sh check           check the deployed exchange
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
readonly MANIFEST_REDIRECT="http://localhost:53682/github-app-created"
readonly MAX_APPLICATIONS=10   # GitHub allows ten callback URLs per App

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

ask_optional() { # ask_optional VARIABLE "question" [default]
  local answer
  read -rp "    $2${3:+ [$3]}: " answer
  printf -v "$1" '%s' "${answer:-${3:-}}"
}

confirm() { local answer; read -rp "    $1 [y/N]: " answer; [[ "$answer" =~ ^[Yy] ]]; }

open_url() { # open_url URL: in the browser where possible; always printed
  note "Opening: $1"
  local opener
  for opener in open xdg-open wslview; do
    if command -v "$opener" >/dev/null; then "$opener" "$1" >/dev/null 2>&1 && return 0; fi
  done
  note "(open it yourself in your browser)"
}

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

# ---- applications -------------------------------------------------------------
# APPS holds one entry per application, separated by ';':  id|origin|callback|repository

application_lines() { tr ';' '\n' <<<"${APPS:-}" | sed '/^$/d'; }
application_count() { application_lines | wc -l | tr -d ' '; }
field() { cut -d'|' -f"$2" <<<"$1"; }   # field ENTRY N

applications_json() {
  local entry entries=()
  while IFS= read -r entry; do
    entries+=("$(printf '{"id":"%s","provider":"github","origins":["%s"],"redirectUris":["%s"],"requiredRepository":"%s"}' \
      "$(field "$entry" 1)" "$(field "$entry" 2)" "$(field "$entry" 3)" "$(field "$entry" 4)")")
  done < <(application_lines)
  local IFS=,
  printf '[%s]' "${entries[*]}"
}

callback_urls_json() {
  local entry urls=()
  while IFS= read -r entry; do urls+=("\"$(field "$entry" 3)\""); done < <(application_lines)
  local IFS=,
  printf '[%s]' "${urls[*]}"
}

show_applications() {
  local entry
  while IFS= read -r entry; do
    note "$(field "$entry" 1): $(field "$entry" 2)  callback $(field "$entry" 3)  data $(field "$entry" 4)"
  done < <(application_lines)
}

migrate_single_application() { # settings written by the first version of this script
  if [[ -z "${APPS:-}" && -n "${APP_ID:-}" ]]; then
    APPS="$APP_ID|$APP_ORIGIN|$APP_CALLBACK|$DATA_REPO"
    save_setting APPS "$APPS"
  fi
}

ask_application() { # prompts for one application and appends it to APPS
  (( $(application_count) < MAX_APPLICATIONS )) || fail "a GitHub App allows $MAX_APPLICATIONS callback URLs; this environment is full"
  local origin callback repo id
  say "Application $(( $(application_count) + 1 ))"
  ask origin "Its origin (https://host, no path or trailing slash)"
  [[ "$origin" =~ ^https://[a-z0-9.-]+(:[0-9]+)?$ ]] || fail "an origin is https://host[:port], lowercase, with nothing after it"
  ask callback "Its sign-in callback URL" "$origin/auth/callback"
  [[ "$callback" == "$origin/"* ]] || fail "the callback URL must start with the origin"
  ask repo "Its data repository (owner/name)"
  [[ "$repo" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]] || fail "a repository is owner/name"
  ask id "Application id (a-z, 0-9, -)" "$(sed 's#https://##; s#[.:].*##' <<<"$origin")-$ENVIRONMENT"
  [[ "$id" =~ ^[a-z0-9-]{1,64}$ ]] || fail "an application id is 1-64 characters of a-z, 0-9 and -"
  ! application_lines | cut -d'|' -f1 | grep -qx "$id" || fail "an application '$id' is already registered"
  APPS="${APPS:+$APPS;}$id|$origin|$callback|$repo"
  save_setting APPS "$APPS"
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
  ACCOUNT="$(aws sts get-caller-identity --query Account --output text)"
  BUCKET="${BUCKET:-fides-artifacts-$ACCOUNT-$REGION}"
  local name; for name in REGION ZONE DOMAIN BUCKET; do save_setting "$name" "${!name}"; done

  migrate_single_application
  if [[ -n "${APPS:-}" ]]; then
    say "Registered applications"
    show_applications
  else
    ask_application
  fi
  while (( $(application_count) < MAX_APPLICATIONS )) && confirm "Add another application now?"; do ask_application; done
}

store_secret() { # store_secret VALUE: writes the client secret to Secrets Manager
  local name="fides/$ENVIRONMENT/github-client-secret" existing file
  existing="$(aws secretsmanager describe-secret --region "$REGION" --secret-id "$name" --query ARN --output text 2>/dev/null || true)"
  file="$(umask 077; mktemp)"
  printf '%s' "$1" > "$file"
  if [[ -n "$existing" ]]; then
    aws secretsmanager put-secret-value --region "$REGION" --secret-id "$name" --secret-string "file://$file" >/dev/null
    SECRET_ARN="$existing"
  else
    SECRET_ARN="$(aws secretsmanager create-secret --region "$REGION" --name "$name" --secret-string "file://$file" --query ARN --output text)"
  fi
  rm -f "$file"
  save_setting SECRET_ARN "$SECRET_ARN"
  note "client secret stored in AWS Secrets Manager ($name)"
}

manifest_json() {
  printf '{"name":"%s","url":"%s","hook_attributes":{"url":"%s","active":false},"redirect_url":"%s","callback_urls":%s,"public":false,"request_oauth_on_install":false,"setup_on_update":false,"default_permissions":{"contents":"write","metadata":"read"},"default_events":[]}' \
    "$APP_NAME" "$HOMEPAGE" "$HOMEPAGE" "$MANIFEST_REDIRECT" "$(callback_urls_json)"
}

create_github_app() { # GitHub's App manifest flow: one click in the browser, the rest here
  say "Create the shared GitHub App '$APP_NAME' (DF-FIDES-2026-0016)"
  ask HOMEPAGE "Homepage URL for the App" "${HOMEPAGE:-https://$ZONE}"
  ask_optional OWNER_ORG "GitHub organization that owns the data repositories (empty for your personal account)" "${OWNER_ORG:-}"
  save_setting HOMEPAGE "$HOMEPAGE"; save_setting OWNER_ORG "$OWNER_ORG"
  local action state page pasted code
  action="https://github.com/settings/apps/new"
  [[ -n "$OWNER_ORG" ]] && action="https://github.com/organizations/$OWNER_ORG/settings/apps/new"
  state="$(od -An -N16 -tx1 /dev/urandom | tr -d ' \n')"
  page="$(umask 077; mktemp -d)/create-github-app.html"
  cat > "$page" <<EOF
<!doctype html><meta charset="utf-8"><title>Create $APP_NAME</title>
<p>Sending the App definition to GitHub&hellip; If nothing happens, press the button.</p>
<form id="f" method="post" action="$action?state=$state">
<input type="hidden" name="manifest" value='$(manifest_json)'>
<button type="submit">Continue to GitHub</button></form>
<script>document.getElementById("f").submit()</script>
EOF
  cat <<EOF
    Your browser will open GitHub with the App already filled in:
      - name $APP_NAME, callback URLs for every registered application
      - Contents: read and write; Metadata: read-only; no webhooks
      - installable only on this account
    Click "Create GitHub App". GitHub then sends you to a localhost address
    that does not load; that is expected. Copy the whole address from the
    address bar and paste it here.
EOF
  open_url "file://$page"
  ask pasted "Paste the address (or just the code)"
  rm -rf "$(dirname "$page")"
  if [[ "$pasted" == *"code="* ]]; then
    [[ "$pasted" == *"state=$state"* ]] || fail "that address does not belong to this run (state mismatch); run init again"
    code="${pasted#*code=}"; code="${code%%&*}"
  else
    code="$pasted"
  fi
  [[ "$code" =~ ^[A-Za-z0-9_-]+$ ]] || fail "that is not a GitHub manifest code"
  local reply=()
  mapfile -t reply < <(gh api -X POST "/app-manifests/$code/conversions" --jq '.client_id, .slug, .client_secret')
  [[ ${#reply[@]} -eq 3 && "${reply[0]}" =~ ^Iv ]] || fail "GitHub did not return the App's credentials (the code is single-use and expires after an hour); run init again"
  CLIENT_ID="${reply[0]}"; APP_SLUG="${reply[1]}"
  save_setting CLIENT_ID "$CLIENT_ID"; save_setting APP_SLUG "$APP_SLUG"
  store_secret "${reply[2]}"
  reply=()
  note "created $APP_NAME (client id $CLIENT_ID)"
  cat <<EOF

    Two checks on the App's settings page (opening it now):
      1. GitHub generated a private key with the App. Fides does not use it:
         under "Private keys", delete it.
      2. Make sure user authorization tokens expire: if the page offers
         "Expire user authorization tokens" (or the optional feature
         "User-to-server token expiration"), it must be enabled. Fides
         refuses non-expiring tokens, so a wrong setting shows up as a
         refused first sign-in, never as a silent risk.
EOF
  open_url "$(app_settings_url)"
  read -rp "    Press Enter when both are done. "
}

app_settings_url() {
  if [[ -n "${OWNER_ORG:-}" ]]; then printf 'https://github.com/organizations/%s/settings/apps/%s' "$OWNER_ORG" "$APP_SLUG"
  else printf 'https://github.com/settings/apps/%s' "$APP_SLUG"; fi
}

use_existing_github_app() {
  say "Using the existing GitHub App ($CLIENT_ID)"
  note "Make sure its Callback URLs include every registered application's callback:"
  local entry; while IFS= read -r entry; do note "  $(field "$entry" 3)"; done < <(application_lines)
  [[ -n "${APP_SLUG:-}" ]] && open_url "$(app_settings_url)"
  if confirm "Store a new client secret for it now?"; then
    local secret
    read -rsp "    Paste the client secret (nothing is shown), then Enter: " secret; echo
    [[ -n "$secret" ]] || fail "no secret was pasted"
    store_secret "$secret"; unset secret
  fi
  [[ -n "${SECRET_ARN:-}" ]] || fail "no client secret is stored for this environment"
}

register_github_app() {
  APP_NAME="${APP_NAME:-Echelon ($ENVIRONMENT)}"
  save_setting APP_NAME "$APP_NAME"
  if [[ -n "${CLIENT_ID:-}" ]] && confirm "Keep using the GitHub App with client id $CLIENT_ID?"; then
    use_existing_github_app
  else
    create_github_app
  fi
}

install_github_app() {
  say "Install the App on the data repositories"
  note "Choose 'Only select repositories' and pick:"
  local entry; while IFS= read -r entry; do note "  $(field "$entry" 4)"; done < <(application_lines)
  if [[ -n "${APP_SLUG:-}" ]]; then open_url "https://github.com/apps/$APP_SLUG/installations/new"
  else note "GitHub: the App's settings page > Install App"; fi
  read -rp "    Press Enter when the App is installed on them. "
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

ensure_template() { # ensure_template VERSION: the release's template, without re-uploading
  [[ -f "$WORK_DIR/src/infrastructure/aws/fides-exchange.template.json" ]] && return 0
  mkdir -p "$WORK_DIR"; rm -rf "$WORK_DIR/src"
  git clone --quiet --depth 1 --branch "v$1" "https://github.com/$REPOSITORY" "$WORK_DIR/src"
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

preflight() { # preflight ORIGIN: the exchange's CORS answer for that origin
  curl -s -o /dev/null -w '%{http_code}' -X OPTIONS "https://$DOMAIN/v1/token" -H "Origin: $1" || true
}

check() {
  say "Checking https://$DOMAIN"
  local entry origin first answer bad failed=0
  first="$(field "$(application_lines | head -1)" 2)"
  for _ in $(seq 1 30); do [[ "$(preflight "$first")" == "204" ]] && break; sleep 10; done
  while IFS= read -r entry; do
    origin="$(field "$entry" 2)"; answer="$(preflight "$origin")"
    note "$origin -> $answer (expect 204)"
    [[ "$answer" == "204" ]] || failed=1
  done < <(application_lines)
  bad="$(preflight "https://not-registered.example")"
  note "unknown origin -> $bad (expect 403)"
  [[ "$bad" == "403" ]] || failed=1
  (( failed == 0 )) || fail "the exchange did not answer as expected; see the function's CloudWatch log group"
  cat <<EOF

    Fides is live. Configure each application's Fides client with:
      exchange URL   https://$DOMAIN
      client id      $CLIENT_ID
      retention      memory-only (the default)
    and its own application id and redirect URI:
EOF
  while IFS= read -r entry; do note "  $(field "$entry" 1): redirect $(field "$entry" 3)"; done < <(application_lines)
  note "Then sign in once from each application."
}

# ---- commands ---------------------------------------------------------------
init() {
  require_tools
  # shellcheck source=/dev/null
  [[ -f "$SETTINGS" ]] && source "$SETTINGS"
  collect_settings
  register_github_app
  install_github_app
  certificate
  release "${VERSION:-$DEFAULT_VERSION}"
  deploy_stack "${VERSION:-$DEFAULT_VERSION}"
  point_dns
  check
}

add_app() {
  require_tools; load_settings; migrate_single_application
  ask_application
  local entry; entry="$(application_lines | tail -1)"
  say "Add the new callback to the GitHub App and install it on the new repository"
  note "Callback URL to add: $(field "$entry" 3)"
  note "Repository to add to the installation: $(field "$entry" 4)"
  [[ -n "${APP_SLUG:-}" ]] && open_url "$(app_settings_url)"
  read -rp "    Press Enter when the callback URL is saved and the repository is added. "
  ensure_template "${VERSION:-$DEFAULT_VERSION}"
  deploy_stack "${VERSION:-$DEFAULT_VERSION}"
  check
}

update() { # update VERSION
  [[ -n "${1:-}" ]] || fail "usage: $0 update VERSION (for example 0.3.0)"
  require_tools; load_settings; migrate_single_application
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
  add-app) add_app ;;
  apps) load_settings; migrate_single_application; show_applications ;;
  update) update "${2:-}" ;;
  check) require_tools; load_settings; migrate_single_application; check ;;
  *) sed -n '2,15p' "$0" | sed 's/^# \{0,1\}//'; exit 2 ;;
esac
