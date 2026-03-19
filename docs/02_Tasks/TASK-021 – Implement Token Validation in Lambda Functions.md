---
type: Task
task_id: TASK-021
title: "Implement Token Validation in Lambda Functions"
owner: STK-001
status: "Completed"
related_milestone: MS-007
---

## Description

Implement JWT token validation and claims-based authorization within the ASP.NET Core Lambda. The Lambda trusts tokens that have already been signature-validated by the API Gateway JWT authorizer (see TASK-020). Inside the Lambda, `Microsoft.AspNetCore.Authentication.JwtBearer` is used to parse the token, extract claims, and feed `JitProvisioningClaimsTransformation` — which maps Cognito identity information into application-level user and organization identities on every authenticated request.

This task is **substantially complete**. The only known gap is the absence of dedicated unit tests for the validation/provisioning logic; this is an acceptable tradeoff for a POC given that k6 integration tests cover the end-to-end authenticated path.

## Implementation Notes

### Architecture: Split Validation Responsibility

Token validation is deliberately split across two layers to avoid redundant cryptographic work inside a Lambda cold-start path:

| Layer | Responsibility |
|---|---|
| **API Gateway** | Signature validation (JWKS fetch + RS256 verify), `iss` / `aud` claim checks, reject unauthenticated calls with `401` before Lambda is invoked |
| **Lambda (JwtBearer)** | Token parsing, claims extraction, `IClaimsTransformation` pipeline, application-level authorization policies |

Because API Gateway has already validated the signature before the request reaches the Lambda, `ValidateIssuerSigningKey` is explicitly disabled in `Program.cs`. This is **intentional and safe** — the Lambda only ever receives tokens that passed API Gateway validation.

### JwtBearer Configuration (Program.cs)

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}";
        options.Audience  = appClientId;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = false,   // API Gateway already validated the signature
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,

            // Accept the token as-is — signature bytes are not re-verified
            SignatureValidator = (token, _) => new JsonWebToken(token)
        };
    });

services.AddAuthorizationBuilder()
    .AddDefaultPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());

// Plugs into the ASP.NET Core auth pipeline — runs after JwtBearer on every request
services.AddScoped<IClaimsTransformation, JitProvisioningClaimsTransformation>();
```

### JIT Provisioning Flow

`JitProvisioningClaimsTransformation.TransformAsync` runs on every authenticated request:

```
Incoming JWT claims
    │
    ▼
Extract: sub, email, name, cognito:groups
    │
    ▼
Parse identity provider from cognito:groups
  e.g. "ap-southeast-1_xxxxx_IDP-A" → "IDP-A"
    │
    ▼
Lookup or create Organization document in DocumentDB
    │
    ▼
Lookup or create User document (sub + idp as composite key)
  • New users get role: Requester by default
  • Existing users: lastAccessAt updated
    │
    ▼
Emit custom claims onto ClaimsPrincipal:
  user_id, user_email, user_organization, user_roles, identity_provider
```

### Custom Claims Available to Endpoints

After transformation, endpoint handlers can read:

| Claim | Source | Example Value |
|---|---|---|
| `sub` | JWT | `IDP-A_A-001` |
| `email` | JWT | `a.alex@org-a.example.com` |
| `name` | JWT | `Alex Kim` |
| `cognito:groups` | JWT | `ap-southeast-1_xxxxx_IDP-A` |
| `user_id` | JIT | MongoDB ObjectId of provisioned User |
| `user_email` | JIT | Same as `email` |
| `user_organization` | JIT | MongoDB ObjectId of Organization |
| `user_roles` | JIT | `Requester` (default) or `DataOwner` |
| `identity_provider` | JIT | `IDP-A` or `IDP-B` |

### Authorization Policies

The default policy (`RequireAuthenticatedUser`) protects all routes via the `$default` catch-all. Organization-scoped authorization (e.g., ensuring a DataOwner only sees their own org's pending approvals) is enforced in endpoint handler logic using the `user_organization` claim — not via named policies.

### Known Gap: Unit Tests

Unit tests for `JitProvisioningClaimsTransformation` and the claims-extraction logic are **not present**. The authentication flow is covered by k6 integration tests (`backend-smoke.js`, `backend-api.js`) against a local backend. For a production system, unit tests against a mocked `IUserRepository` / `IOrganizationRepository` would be required. This gap is **accepted for this POC**.

## Definition of Done

- [x] `Microsoft.AspNetCore.Authentication.JwtBearer` integrated and configured in `Program.cs`
- [x] Signature validation intentionally disabled (`ValidateIssuerSigningKey = false`) with `SignatureValidator` pass-through — API Gateway owns this responsibility
- [x] `iss`, `aud`, and `exp` claims still validated by JwtBearer inside the Lambda
- [x] `JitProvisioningClaimsTransformation` implemented — runs on every authenticated request
- [x] Claims extraction: `sub`, `email`, `name`, `cognito:groups` parsed from JWT
- [x] Identity provider derived from `cognito:groups` federated group name
- [x] Organization auto-created in DocumentDB if first user from that IdP
- [x] User auto-created with `Requester` role; `lastAccessAt` updated on subsequent visits
- [x] Custom claims (`user_id`, `user_organization`, `user_roles`, `identity_provider`) injected into `ClaimsPrincipal`
- [x] k6 integration tests validate JIT provisioning end-to-end (`backend-smoke.js` — "JIT Provisioning - New User Creation" group)
- [x] k6 tests validate identity isolation — two users named "Alex Kim" from different IdPs resolve to different organizations
- [ ] Unit tests for `JitProvisioningClaimsTransformation` *(known gap — acceptable for POC)*
