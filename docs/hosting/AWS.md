---
id: FIDES-HOSTING-AWS
title: Deploying the Fides exchange to AWS
status: accepted
version: 1.0.0
created: 2026-10-08
updated: 2026-10-08
owners:
  - fides
related_documents:
  - infrastructure/aws/fides-exchange.template.json
  - research/decisions/DF-FIDES-2026-0001--cloud-agnostic-exchange-aws-first.md
  - research/decisions/DF-FIDES-2026-0003--github-app-user-to-server-tokens.md
  - research/decisions/DF-FIDES-2026-0005--deployment-is-operator-configuration.md
tags: [hosting, aws, deployment, github-app]
provenance:
  contributions:
    EXE-20261008T091048263Z-e6a3dfca:
      operations: [created]
      at: 2026-10-08T09:31:58.521Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Operator deployment requirements for the AWS adapter (WI-0008, DF-FIDES-2026-0005)"
---

# Deploying the Fides exchange to AWS

Nothing in this repository deploys anything (DF-FIDES-2026-0005). This page
lists what the operator must provide and the commands that would deploy one
environment. Deploy each environment (`test`, `staging`, `production`) as its
own stack with its own GitHub App (FID-HOST-003).

## What the operator must provide

### 1. A GitHub App per environment

Register at **GitHub → Settings → Developer settings → GitHub Apps → New
GitHub App** (under the organization that owns the data repositories, if
there is one).

| Field | Value |
|---|---|
| GitHub App name | For example `Echelon Sign-in (production)`; unique on GitHub |
| Homepage URL | The applications' public site |
| Callback URL | Every redirect URI of every application in this environment, for example `https://chrona.example/auth/callback` (GitHub allows up to 10) |
| Expire user authorization tokens | **Checked** (required: Fides refuses non-expiring tokens) |
| Request user authorization (OAuth) during installation | Unchecked |
| Enable Device Flow | Unchecked |
| Setup URL, Post installation redirect | Empty |
| Webhook → Active | **Unchecked** (Fides uses no webhooks) |
| Repository permissions | **Contents: Read and write** (what Arca needs); **Metadata: Read-only** (mandatory) |
| Organization and account permissions | None |
| Where can this GitHub App be installed? | **Only on this account** |

Then:

1. Note the **Client ID** (starts with `Iv`). It is public. The App ID and
   private key are not used by Fides; do not generate a private key.
2. **Generate a new client secret.** Copy it once, straight into AWS Secrets
   Manager (step 3 below). Never paste it into a file, chat or repository.
3. **Install the App** on the data repositories each application uses (for
   example `owner/chrona-data`). The installation bounds what any user's
   token can reach (FID-TB-004).

### 2. AWS account, region and domain

| Item | Notes |
|---|---|
| AWS account | Any account; the template hard-codes none. |
| Region | Any region with Lambda arm64 and API Gateway HTTP APIs. The secret, certificate and stack must all be in it. |
| Domain name | The exchange's host name, for example `auth.example.com`. |
| ACM certificate | An **issued** certificate for that host name **in the same region** (regional endpoint, not `us-east-1`-only), DNS-validated. |
| DNS | Ability to add a CNAME (or Route 53 alias) from the host name to the stack's `RegionalDomainName` output. |
| S3 bucket | Holds the function package; the deploying identity needs `s3:PutObject` there. |
| Deploying identity | Permission to create the template's resources with `CAPABILITY_IAM` (IAM role, Lambda, API Gateway, Logs, Lambda permission). |

### 3. The secret

Create one Secrets Manager secret per environment whose **SecretString is
exactly the GitHub App client secret** (plain text, no JSON):

```bash
aws secretsmanager create-secret \
  --region "$REGION" \
  --name "fides/$ENVIRONMENT/github-client-secret" \
  --secret-string "$(read -rs s; printf %s "$s")"   # paste, then Enter
```

Use the default `aws/secretsmanager` key. If you use a customer-managed KMS
key instead, add `kms:Decrypt` on that key to the function role. The
template only ever sees the secret's **ARN** (`ClientSecretArn`). Rotate by
generating a new client secret on GitHub and updating the secret's value;
warm functions pick it up within five minutes.

### 4. The application registrations

`ApplicationsJson` is a JSON array, one entry per application in this
environment:

```json
[
  {
    "id": "chrona-production",
    "provider": "github",
    "origins": ["https://chrona.example"],
    "redirectUris": ["https://chrona.example/auth/callback"],
    "requiredRepository": "owner/chrona-data"
  }
]
```

- `origins` are exact `https://host[:port]` origins; `redirectUris` are exact
  absolute `https` URIs; both are compared character for character.
- Every redirect URI must also be a Callback URL of the GitHub App.
- `requiredRepository` is optional. When set, a person whose token cannot
  read that repository is refused at sign-in (`repository_access_denied`).
- `id` is 1-64 characters of `a-z`, `0-9` and `-`.

## Template parameters

| Parameter | Example | Secret? |
|---|---|---|
| `EnvironmentName` | `production` | no |
| `DomainName` | `auth.example.com` | no |
| `CertificateArn` | `arn:aws:acm:REGION:ACCOUNT:certificate/…` | no |
| `GitHubClientId` | `Iv23li…` | no |
| `ClientSecretArn` | `arn:aws:secretsmanager:REGION:ACCOUNT:secret:fides/production/github-client-secret-…` | no (a reference) |
| `ApplicationsJson` | see above | no |
| `CodeBucket`, `CodeKey` | `my-artifacts`, `fides/0.1.0/fides-exchange-linux-arm64.zip` | no |
| `ThrottleRatePerSecond`, `ThrottleBurst` | `20`, `40` (defaults) | no |
| `LogRetentionDays` | `30` (default) | no |

## Build and deploy (for the operator; not run here)

```bash
# Package the function: a self-contained linux-arm64 executable named bootstrap.
dotnet publish src/Fides.Hosting.Aws/Fides.Hosting.Aws.fsproj -c Release \
  -r linux-arm64 --self-contained -o dist/lambda
(cd dist/lambda && zip -qr ../fides-exchange-linux-arm64.zip .)
aws s3 cp dist/fides-exchange-linux-arm64.zip "s3://$BUCKET/fides/$VERSION/fides-exchange-linux-arm64.zip"

# Deploy one environment.
aws cloudformation deploy \
  --region "$REGION" \
  --stack-name "fides-exchange-$ENVIRONMENT" \
  --template-file infrastructure/aws/fides-exchange.template.json \
  --capabilities CAPABILITY_IAM \
  --parameter-overrides \
    EnvironmentName="$ENVIRONMENT" DomainName="$DOMAIN" CertificateArn="$CERTIFICATE_ARN" \
    GitHubClientId="$CLIENT_ID" ClientSecretArn="$SECRET_ARN" \
    ApplicationsJson="$(cat applications.$ENVIRONMENT.json)" \
    CodeBucket="$BUCKET" CodeKey="fides/$VERSION/fides-exchange-linux-arm64.zip"

# Point DNS at the API.
aws cloudformation describe-stacks --region "$REGION" --stack-name "fides-exchange-$ENVIRONMENT" \
  --query "Stacks[0].Outputs[?OutputKey=='RegionalDomainName'].OutputValue" --output text
```

Applications then use `https://$DOMAIN` as their Fides exchange URL.

## What the stack contains

- A Lambda function on `provided.al2023`, arm64, 256 MB, 15 s timeout, with
  its configuration in `FIDES_CONFIGURATION` (no secret, only the secret's
  ARN). An invalid configuration stops the function at start-up.
- An IAM role that can write the function's own log group and read exactly
  the one secret.
- An HTTP API with only the six protocol routes (`POST` and `OPTIONS` on
  `/v1/token`, `/v1/refresh`, `/v1/revoke`), throttling, access logs without
  client IP addresses, the default `execute-api` endpoint disabled, and a
  regional custom domain with TLS 1.2. CORS is answered by the function's
  exact allow-list, not by API Gateway.
- Log groups with bounded retention. Logs carry only the operation, the
  registered application, the outcome code and the duration.

## Verification without deploying

- `dotnet test` runs the adapter in process against simulated GitHub over
  real HTTP, and runs the built `bootstrap` as a process against an
  emulated Lambda Runtime API.
- `cfn-lint infrastructure/aws/fides-exchange.template.json` validates the
  template (CI runs it).
