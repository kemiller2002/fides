---
id: FIDES-UI-REQUIREMENTS
title: Fides Forma UI requirements and screen inventory
status: draft
version: 0.1.0
created: 2026-11-09
updated: 2026-11-09
owners:
  - fides
related_documents:
  - docs/requirements/FIDES-REQUIREMENTS.md
  - docs/architecture/EXCHANGE-PROTOCOL.md
tags: [ui, forma, limen, accessibility, screens]
provenance:
  contributions:
    EXE-20261109T142541284Z-a3f0d18d:
      operations: [created]
      at: 2026-11-09T14:44:52.332Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Fides requirements expansion and security architecture review (WI-0022)"
---

# Fides UI requirements and screen inventory

Fides has **no UI today**: the client is headless and each application
would draw its own sign-in. The normative requirements are FID-UX-001..015
in the catalogue; all are `missing`, `blocked` or `deferred` except
FID-UX-001 (`implemented-unverified`). Before building any screen, follow
the Visual Engineering and Percepta instructions in `AGENTS.md`; this
inventory is the input for the Percepta screen contracts.

## 1. Inputs from sibling components (checked 2026-11-09)

| Component | Version in echelon-current | What Fides uses |
|---|---|---|
| Forma | 0.14.0 | Brand manifests (`--ef-*` tokens); `spinner`, `skeleton`, `progress-bar`, `.ef-startup`; fault components; WCAG 2.2 AA target (A11Y-001). **Its default palette is still Echelon's**, so Fides must always supply a brand manifest (FID-UX-003) |
| Limen | 0.9.1 / limen-fsharp 0.9.0 | Route inventory, `captureReturnTo` and `resumeReturnTo`, credential-parameter refusal, replace navigation, page lifecycle |
| Aegis | 1.0.0 | No browser sink: client faults cannot be classified through Aegis yet (FID-UX-012) |
| Folio | 0.4.1 | Not used: no printable document is required (FID-UX-011) |

## 2. Screen inventory

Every screen is a Forma layout with the Fides brand manifest (or the
application's), a heading that receives focus, and a live region that
announces the state.

| Screen | Shown when | Primary question it answers | Content and actions | States | Requirements |
|---|---|---|---|---|---|
| S1 Landing | A person opens Fides directly | What is Fides? | Logo, purpose, what signing in shares, privacy and support links | default | FID-UX-004 |
| S2 Sign-in | Signed out, or `expired`, in an application | How do I sign in? | Application name, "Sign in with GitHub" (top-level navigation), what will be shared, retention choice only if the application enables persistence | default; pending (button `aria-busy`) | FID-UX-005, FID-UX-013, FID-SES-008 |
| S3 Redirecting | Between the click and GitHub | Is anything happening? | Branded indicator, "Taking you to GitHub" | pending; slow (after 3 s, show the provider address) | FID-UX-006 |
| S4 Callback in progress | Callback page while the exchange runs | Am I being signed in? | Branded indicator, no application content | pending; slow; timed out → S8 | FID-UX-005, FID-UX-006 |
| S5 Sign-in could not be completed | `state_invalid`, `state_expired`, `code_rejected`, `malformed_request`, `invalid_code_verifier` | What went wrong, what now? | "Sign-in could not be completed. Start again." | default | FID-UX-007, FID-UX-010 (authentication failure) |
| S6 Sign-in cancelled | `provider_denied` | Did I cancel? | "You did not authorize the application." Retry | default | FID-UX-007 |
| S7 Not permitted | `repository_access_denied`; application `NotPermitted` | Am I signed in but not allowed? | Signed-in identity, what access is missing, who to ask, sign out | default | FID-UX-010 (authorization denial) |
| S8 Service unavailable | `provider_unavailable`, `internal_error`, timeout, `configuration_unavailable`, `provider_contract_violation` | Is this my fault? | "GitHub or the sign-in service is unavailable", retry, reference id (FID-OPS-004) | default; retrying | FID-UX-010 (provider outage) |
| S9 Session expired | `expired` | Why was I signed out? | "Your session ended", sign in again and return to the route | default | FID-UX-007, FID-UX-015 |
| S10 Session revoked | `revoked` | Why was I signed out? | "Your access was removed or you signed out elsewhere", sign in again | default | FID-UX-007 |
| S11 Signing out | Sign-out in progress | Am I signed out? | Indicator; then S12 | pending | FID-UX-006 |
| S12 Signed out | After sign-out | Am I safe to leave? | Confirmation; if revocation failed, say so (FID-TB-005) | default; revocation failed | FID-UX-007 |
| S13 Expiry warning | Before maximum lifetime | Will I lose work? | Non-modal warning, re-authenticate now | default | FID-UX-015 |

## 3. Failure-class mapping (FID-UX-010)

| Class | States and refusals | Wording rule |
|---|---|---|
| Authentication failure | `state_invalid`, `state_expired`, `provider_denied`, `code_rejected`, `malformed_request`, `invalid_code_verifier`, `identity_revoked`, `expired`, `revoked`, `refresh_rejected` | Ask the person to sign in; never say "not permitted" |
| Authorization denial | `repository_access_denied`, `origin_not_allowed`, `redirect_uri_not_allowed`, `unknown_application`, application `NotPermitted` | Say they are signed in (when they are) and what is not allowed; never suggest signing in again as the fix |
| Provider or service outage | `provider_unavailable`, `provider_contract_violation`, `configuration_unavailable`, `internal_error`, transport failure | Say it is temporary and not their fault; offer retry |

`origin_not_allowed`, `redirect_uri_not_allowed` and `unknown_application`
are configuration errors of the application; they are shown as denial to
the person and reported as faults to the operator.

## 4. Loading rules (FID-UX-006)

- Every wait listed in S3, S4 and S11 shows a Forma indicator with an
  accessible name within 100 ms. A blank frame or bare "Loading…" text is a
  defect.
- After 3 s the indicator gains an explanation; after the protocol timeout
  (FID-REL-009) the flow moves to S8.
- Waits that do not block the person (background refresh) show nothing.

## 5. Accessibility (FID-UX-008, FID-UX-009)

WCAG 2.2 AA, including focus not obscured (2.4.11), target size (2.5.8),
consistent help (3.2.6) and accessible authentication (3.3.8: no cognitive
test; Fides defers to GitHub's sign-in, which must not be embedded).
Keyboard-only and screen-reader walkthroughs are recorded per release.

## 6. Verification

Rendered verification through Dokimos requirement verification
(`data-requirement-id` hooks from Forma's `RequirementIdentity`) once the
pins are upgraded (FID-TEST-014), plus Percepta screen contracts per
AGENTS.md. Manual accessibility audits are evidence of their own level
(`manual-accessibility`).
