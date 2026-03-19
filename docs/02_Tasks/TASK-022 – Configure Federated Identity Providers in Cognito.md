---
type: Task
task_id: TASK-022
title: "Configure Federated Identity Providers in Cognito"
owner: STK-001
status: "Completed"
related_milestone: MS-007
---

## Description

Configure federated OIDC identity providers (IDP-A and IDP-B) in the Cognito User Pool so that external collaborators can authenticate via their simulated organizational identity providers. Cognito acts as an identity broker: it accepts OIDC tokens from each IdP, maps their claims into a normalised Cognito identity, issues its own JWT, and then the user proceeds through the standard API Gateway → Lambda auth flow.

Both providers are **fully configured** in Terraform under `infrastructure/terraform/modules/cognito/main.tf`. No manual Cognito Console steps are required.

## Implementation Notes

### How the `idp_providers` Variable Wires the IDP Lambda URLs

The Cognito module accepts an `idp_providers` list variable. The root `main.tf` populates this list with the Lambda function URLs of the two simulated OIDC IdP services (implemented in TASK-011):

```hcl
# root/main.tf (illustrative)
module "cognito" {
  source = "./modules/cognito"

  idp_providers = [
    {
      provider_name = "IDP-A"
      provider_type = "OIDC"
      client_id     = var.idp_a_client_id
      client_secret = var.idp_a_client_secret
      issuer_url    = module.idp_a_lambda.function_url   # e.g. https://<id>.lambda-url.<region>.on.aws
    },
    {
      provider_name = "IDP-B"
      provider_type = "OIDC"
      client_id     = var.idp_b_client_id
      client_secret = var.idp_b_client_secret
      issuer_url    = module.idp_b_lambda.function_url
    }
  ]
}
```

### Terraform Configuration (modules/cognito/main.tf)

```hcl
# Iterate over the idp_providers list to create one provider per IdP
resource "aws_cognito_identity_provider" "oidc" {
  for_each      = { for idp in var.idp_providers : idp.provider_name => idp }
  user_pool_id  = aws_cognito_user_pool.main.id
  provider_name = each.key                        # "IDP-A" or "IDP-B"
  provider_type = each.value.provider_type        # "OIDC"

  provider_details = {
    client_id     = each.value.client_id
    client_secret = each.value.client_secret
    oidc_issuer   = each.value.issuer_url         # Lambda Function URL of simulated IdP
    authorize_scopes = "openid email profile"
  }

  # Map IdP token claims → Cognito user pool attributes
  attribute_mapping = {
    email    = "email"
    username = "sub"    # IdP subject claim becomes Cognito username
    name     = "name"
  }
}

# App client lists both IdPs as supported providers
resource "aws_cognito_user_pool_client" "main" {
  # ...
  supported_identity_providers = [
    for idp in aws_cognito_identity_provider.oidc : idp.provider_name
  ]
  allowed_oauth_flows  = ["code"]
  allowed_oauth_scopes = ["openid", "email", "profile"]
}
```

### Attribute Mapping Detail

| IdP Token Claim | Cognito Attribute | Notes |
|---|---|---|
| `sub` | `username` | Cognito prefixes this with the provider name: `IDP-A_<sub>` |
| `email` | `email` | Used for JIT provisioning and display |
| `name` | `name` | Display name in the application |

The `cognito:username` claim in the issued JWT takes the form `IDP-A_<original-sub>`, which is how `JitProvisioningClaimsTransformation` identifies which organization a user belongs to.

### Federated Login Flow

```
User's browser
    │ 1. Navigate to Cognito Hosted UI
    ▼
Cognito Hosted UI (/oauth2/authorize)
    │ 2. User selects "Sign in with IDP-A"
    ▼
Simulated IDP-A Lambda (OIDC /authorize endpoint)
    │ 3. User authenticates; IdP issues authorization code
    ▼
Cognito callback (/oauth2/idpresponse)
    │ 4. Cognito exchanges code for IdP tokens
    │ 5. Cognito maps claims; creates/updates federated user in User Pool
    │ 6. Cognito issues its own JWT (id_token + access_token)
    ▼
SPA / Client receives Cognito tokens
    │ 7. Attach id_token as Bearer header on API calls
    ▼
API Gateway JWT Authorizer → Lambda
```

### Testing the Federation (Manual Procedure)

1. **Get the Hosted UI URL** from Terraform output:
   ```bash
   terraform output cognito_hosted_ui_url
   # Returns: https://<domain>.auth.<region>.amazoncognito.com/login?
   #          client_id=<id>&response_type=code&scope=openid+email+profile
   #          &redirect_uri=<callback-url>
   ```

2. **Open the URL in a browser.** You should see the Cognito Hosted UI with "Sign in with IDP-A" and "Sign in with IDP-B" buttons.

3. **Select IDP-A**, authenticate with a test user credential (e.g., `A-001` / `password123` on the simulated IdP).

4. **Capture the authorization code** from the redirect URI callback and exchange it for tokens:
   ```bash
   curl -X POST https://<domain>.auth.<region>.amazoncognito.com/oauth2/token \
     -H "Content-Type: application/x-www-form-urlencoded" \
     -d "grant_type=authorization_code" \
     -d "client_id=<app-client-id>" \
     -d "code=<auth-code>" \
     -d "redirect_uri=<callback-url>"
   ```

5. **Decode the returned `id_token`** (e.g., paste into [jwt.io](https://jwt.io)) and verify:
   - `iss` matches the Cognito User Pool endpoint
   - `cognito:username` is `IDP-A_<sub>`
   - `cognito:groups` contains `ap-southeast-1_<pool-id>_IDP-A`
   - `email` matches the test user

6. **Call a protected API endpoint** with the id_token and confirm a `200` response with correct `organizationId` and `identityProvider` fields.

7. **Repeat steps 3–6 for IDP-B** and confirm a different `organizationId` is returned.

### Verification via AWS CLI

```bash
# List configured identity providers for the user pool
aws cognito-idp list-identity-providers \
  --user-pool-id <pool-id> \
  --query 'Providers[*].{Name:ProviderName,Type:ProviderType}'

# Expected output:
# [
#   { "Name": "IDP-A", "Type": "OIDC" },
#   { "Name": "IDP-B", "Type": "OIDC" }
# ]

# Describe a specific provider to verify attribute mappings
aws cognito-idp describe-identity-provider \
  --user-pool-id <pool-id> \
  --provider-name IDP-A \
  --query 'IdentityProvider.AttributeMapping'
```

## Definition of Done

- [x] `aws_cognito_identity_provider.oidc` resource defined in Terraform with `for_each` over `idp_providers`
- [x] Both IDP-A and IDP-B wired as OIDC providers pointing to their respective Lambda Function URLs
- [x] Attribute mapping configured: `email → email`, `sub → username`, `name → name`
- [x] App client's `supported_identity_providers` dynamically includes all configured IdPs
- [x] OAuth2 flows (`code`) and scopes (`openid`, `email`, `profile`) configured on the app client
- [x] Terraform deployed — both providers visible in AWS Cognito Console
- [x] Verified via AWS CLI: `list-identity-providers` returns IDP-A and IDP-B with type OIDC
- [ ] Manual end-to-end test: Hosted UI federated login for IDP-A produces valid Cognito JWT *(run after deployment)*
- [ ] Manual end-to-end test: Hosted UI federated login for IDP-B produces valid Cognito JWT with different `cognito:groups` value *(run after deployment)*
- [ ] Cross-provider isolation confirmed: IDP-A token resolves to ORG-A; IDP-B token resolves to ORG-B *(verified via `GET /me`)*
