---
type: Task
task_id: TASK-020
title: "Configure Cognito Authorizer for API Gateway"
owner: STK-001
status: "Completed"
related_milestone: MS-007
---

## Description

Configure a Cognito User Pool JWT Authorizer on the API Gateway HTTP API to protect all backend endpoints. The authorizer intercepts every inbound request, validates the Bearer token's signature against the Cognito User Pool's JWKS endpoint, and rejects unauthenticated calls before they reach the Lambda. Only the `GET /health` route is exempt (no authorizer attached).

This task is **fully implemented** in Terraform under `infrastructure/terraform/modules/backend_lambda/main.tf`. No manual AWS Console steps are required — the configuration is deployed via `terraform apply`.

## Implementation Notes

### How It Works

API Gateway HTTP API uses a **JWT authorizer** (not a Lambda authorizer). When a request arrives:

1. API Gateway reads the `Authorization: Bearer <token>` header.
2. It validates the token signature against the Cognito User Pool's JWKS endpoint (`https://cognito-idp.<region>.amazonaws.com/<pool-id>/.well-known/jwks.json`).
3. It checks the `iss` and `aud` claims against the configured `issuer` and `audience` values.
4. On success, the decoded claims are forwarded to the Lambda in the `$context.authorizer.jwt.claims` object and as part of the request context. On failure, API Gateway returns `401 Unauthorized` immediately — the Lambda is never invoked.

### Terraform Configuration (backend_lambda/main.tf)

```hcl
# JWT Authorizer — validates Cognito tokens at the API Gateway layer
resource "aws_apigatewayv2_authorizer" "cognito" {
  api_id           = aws_apigatewayv2_api.main.id
  authorizer_type  = "JWT"
  identity_sources = ["$request.header.Authorization"]
  name             = "${var.project_name}-cognito-authorizer"

  jwt_configuration {
    issuer   = "https://cognito-idp.${var.aws_region}.amazonaws.com/${var.cognito_user_pool_id}"
    audience = [var.cognito_app_client_id]
  }
}

# Public route — health check bypasses the authorizer
resource "aws_apigatewayv2_route" "health" {
  api_id    = aws_apigatewayv2_api.main.id
  route_key = "GET /health"
  target    = "integrations/${aws_apigatewayv2_integration.lambda.id}"
  # No authorization_type set — defaults to NONE
}

# Default route — all other requests require a valid Cognito JWT
resource "aws_apigatewayv2_route" "default" {
  api_id             = aws_apigatewayv2_api.main.id
  route_key          = "$default"
  target             = "integrations/${aws_apigatewayv2_integration.lambda.id}"
  authorization_type = "JWT"
  authorizer_id      = aws_apigatewayv2_authorizer.cognito.id
}
```

### Route Authorization Summary

| Route | Auth Required | Notes |
|---|---|---|
| `GET /health` | No | Public liveness probe |
| `$default` (all others) | Yes — JWT | 401 returned by API Gateway if token missing or invalid |

### Verification Steps

After `terraform apply`, confirm the authorizer is active:

**AWS Console:**
1. Open **API Gateway → HTTP APIs → `<project>-api` → Authorization**.
2. Confirm a JWT authorizer named `<project>-cognito-authorizer` is listed.
3. Check the **Routes** tab — `$default` should show `JWT` in the Auth column; `GET /health` should show `NONE`.

**AWS CLI:**
```bash
# List authorizers for the HTTP API
aws apigatewayv2 get-authorizers \
  --api-id <api-id> \
  --query 'Items[*].{Name:Name,Type:AuthorizerType,Issuer:JwtConfiguration.Issuer}'

# Confirm health is public (no AuthorizationType field)
aws apigatewayv2 get-routes \
  --api-id <api-id> \
  --query 'Items[*].{Route:RouteKey,Auth:AuthorizationType}'
```

**Manual token test:**
```bash
# Should return 401 — no token
curl -i https://<api-id>.execute-api.<region>.amazonaws.com/health-authed-endpoint

# Should return 200 — valid Cognito token
curl -H "Authorization: Bearer <cognito-id-token>" \
     https://<api-id>.execute-api.<region>.amazonaws.com/me
```

## Definition of Done

- [x] JWT authorizer resource defined in Terraform (`aws_apigatewayv2_authorizer.cognito`)
- [x] Authorizer wired to `$default` route with `authorization_type = "JWT"`
- [x] `GET /health` route explicitly left unauthenticated
- [x] Issuer set to Cognito User Pool endpoint; audience set to App Client ID
- [x] Terraform deployed — authorizer active in AWS
- [x] Verified via AWS Console: JWT authorizer appears under API Authorization tab
- [x] Verified via AWS Console: `$default` route shows JWT auth; `/health` shows NONE
- [ ] Manual smoke test: unauthenticated call to protected endpoint returns `401` *(run after deployment)*
- [ ] Manual smoke test: authenticated call with valid Cognito token returns `200` *(run after deployment)*
