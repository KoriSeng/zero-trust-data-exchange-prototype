# OIDC Simulation Plan (Design Baseline Input)

## 1. Purpose
Define how the project will simulate at least two OIDC identity sources for Cognito federation, including fixed test populations and collision cases, while acknowledging limitations.

This document is a planning reference. Authority for scope remains with DR-001.

## 2. Scope
### 2.1 In scope
- Two distinct OIDC issuers (Issuer A and Issuer B)
- AWS-hosted simulation of each issuer (controlled, repeatable deployment)
- Fixed user populations for each issuer
- Active and deactivated users
- Collision test cases that demonstrate issuer+subject deconfliction
- Client configuration using client_id and client_secret where applicable

### 2.2 Out of scope
- Full fidelity enterprise IdP behaviours (e.g., complex MFA policies, advanced key rotation scenarios beyond minimum viable simulation)
- Real institutional identities

## 3. Hosting Approach (AWS-hosted simulation)
### 3.1 Preferred approach: Lambda Function URLs per issuer
Each issuer is simulated by a small OIDC “issuer service” deployed to AWS Lambda and exposed via a Lambda Function URL.

Rationale:
- Controlled environment and repeatable provisioning (aligns to REQ-013).
- Stable, known HTTPS endpoints that can be injected into Cognito configuration.
- Minimal infrastructure footprint compared to a full IdP stack.

### 3.2 Fallback: API Gateway + Lambda
If Function URL behaviour is too limiting for the required OIDC endpoints or redirect handling, each issuer may instead be exposed via its own API Gateway.

## 4. Minimum OIDC Endpoint Surface
For a Cognito OIDC federation integration, each simulated issuer should support (at minimum):
- `/.well-known/openid-configuration` (discovery document)
- `jwks_uri` endpoint returning JWKS
- `authorization_endpoint` (redirect-based)
- `token_endpoint`

Note: The simulation may intentionally simplify features (e.g., limited scopes, simplified consent), but the endpoint shapes must remain OIDC-compatible.

## 4.1 Login UI (Simulated)
Each issuer will provide a minimal login page for username/password entry as part of the authorization flow.

This is intentionally basic and uses synthetic test credentials only (see RISK-010 and CTRL-015).

## 4.2 Pre-integration gate: k6 validation
Before wiring the simulated issuers into Cognito federation configuration, the issuers will be validated in isolation using automated k6 smoke tests (REQ-018, CTRL-016).

## 5. Issuer Identity Sources
### 5.1 Issuer A (Simulated)
- Issuer identifier (`iss`): derived from deployed HTTPS base URL (Terraform output)
- Intended role: “Organisation A” archetype

### 5.2 Issuer B (Simulated)
- Issuer identifier (`iss`): derived from deployed HTTPS base URL (Terraform output)
- Intended role: “Organisation B” archetype

## 6. Fixed Test User Populations
Define a fixed set of users per issuer.

Minimum recommended structure:
- 2 active users per issuer
- 1 deactivated user per issuer
- 1 collision case across issuers (same display name)
- Optional: overlapping `sub` values across issuers to prove issuer+subject deconfliction

Example matrix (IDs are illustrative placeholders, not authoritative):

| Issuer | User Label | Status | Display Name | Subject (`sub`) | Notes |
|---|---|---|---|---|---|
| A | A-User-01 | Active | Alex Kim | A-001 | Normal |
| A | A-User-02 | Active | Sam Lee | A-002 | Normal |
| A | A-User-03 | Deactivated | Pat Chen | A-003 | Offboarding test |
| B | B-User-01 | Active | Alex Kim | B-001 | Collision display name |
| B | B-User-02 | Active | Jamie Tan | B-002 | Normal |
| B | B-User-03 | Deactivated | Riley Ng | B-003 | Offboarding test |

## 7. Collision and Deconfliction Tests
### 7.1 Collision: Same display name across issuers
Expected behaviour:
- System treats identities as distinct.
- Logs and audit trail include the issuer (`iss`) and subject (`sub`).

### 7.2 Optional collision: same `sub` across issuers
Expected behaviour:
- System treats identities as distinct due to different issuer.

## 8. Client Authentication and Secret Handling
- Each simulated issuer must require a client identifier.
- If a client secret is used, it must not be committed to the repository.
- Terraform will wire issuer endpoints into Cognito configuration using outputs.
- Secrets must be injected at deploy time (e.g., Terraform variables via environment or local tfvars not committed).

## 9. Redirect URI and “Stable URL” Considerations
Federation flows require stable redirect URIs and stable issuer identifiers:
- Cognito callback URLs must be known and configured.
- Simulated issuers must issue tokens with an `iss` value that matches their configured issuer base URL.
- URLs produced by Terraform must be treated as authoritative for configuration wiring.

## 10. Limitations / Representativeness
This simulation is intentionally constrained (see RISK-008). Limitations must be documented and must not be over-claimed.

## 11. Traceability
- Requirements: REQ-014, REQ-015, REQ-016, REQ-017, REQ-018
- Risks: RISK-008, RISK-009, RISK-010
- Controls: CTRL-012, CTRL-013, CTRL-014, CTRL-015, CTRL-016
- Milestone: MS-002
- Tasks: TASK-009, TASK-010
