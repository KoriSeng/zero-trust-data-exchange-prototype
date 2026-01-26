---
type: Milestone
milestone_id: MS-003
milestone_name: "Implementation of Main Terraform"
milestone_type: "Implementation"
status: "Complete"
target_date: 2026-02-08
start_date: 2026-02-02
completion_date: 2026-01-26
related_decisions: [DR-001]
related_tasks: [TASK-006, TASK-008, TASK-011]
---

## Objective

Implement the main Terraform to set up the S3 buckets, Cognito, configuration of Cognito to the function URL, and the actual deployment of the IDP.

## Deliverables

- ✅ IDP deployed as Lambda functions with public Function URLs
- ✅ Cognito setup and configured with federated OIDC authentication
- ✅ Function URL linked to Cognito
- ✅ Modular Terraform architecture (oidc_idp, s3_poc, cognito modules)
- ✅ Smoke tests for Lambda IDPs
- ✅ Updated requirements and tasks discovered during delivery

**Note**: S3 bucket configuration and poison tag policy details are documented in [MS-004](MS-004%20-%20S3%20Policy%20Configuration%20for%20Poison.md)

## Implementation Details

### Architecture

The implementation uses a modular Terraform structure with three primary modules:

1. **oidc_idp module** (`infrastructure/terraform/modules/oidc_idp/`)
   - Deploys OIDC Identity Providers as AWS Lambda functions
   - Creates Lambda Function URLs with public access
   - Packages Node.js Express applications with dependencies
   - Configurable runtime, memory, timeout, and environment variables

2. **s3_poc module** (`infrastructure/terraform/modules/s3_poc/`)
   - Creates S3 bucket for data exchange
   - Detailed configuration in [MS-004 - S3 Policy Configuration for Poison](MS-004%20-%20S3%20Policy%20Configuration%20for%20Poison.md)

3. **cognito module** (`infrastructure/terraform/modules/cognito/`)
   - Deploys Cognito User Pool with Hosted UI
   - Configures federated OIDC authentication (IDP-A and IDP-B)
   - Disables native username/password authentication
   - Configures OAuth 2.0 code flow with callback URLs

### Deployed Resources

#### Lambda OIDC Identity Providers

- **IDP-A**: Node.js 22.x Lambda function with public Function URL
  - Endpoint: `https://{function-url}.lambda-url.ap-southeast-1.on.aws/`
  - OIDC Discovery: `/.well-known/openid-configuration`
  - JWKS Endpoint: `/jwks`
  - Authorization Endpoint: `/authorize`

- **IDP-B**: Node.js 22.x Lambda function with public Function URL
  - Same endpoint structure as IDP-A
  - Separate issuer and user population

#### S3 Bucket

See [MS-004 - S3 Policy Configuration for Poison](MS-004%20-%20S3%20Policy%20Configuration%20for%20Poison.md) for detailed S3 configuration, policy implementation, and testing.

#### Cognito User Pool

- **User Pool**: `{project-name}-{environment}`
  - Federated authentication only (no native Cognito users)
  - Self-registration disabled
  - Email-based usernames from federated providers
  - Hosted UI domain: `{project-name}-{environment}`

- **Identity Providers**: IDP-A and IDP-B configured as OIDC providers
  - Authorization code flow
  - Email, profile, and openid scopes
  - GET method for attributes request

- **App Client**: OAuth 2.0 client for SPA integration
  - Client ID and secret available via Terraform outputs
  - Callback URLs: `http://localhost:3000/callback`, `https://localhost:3000/callback`
  - Logout URLs: `http://localhost:3000`, `https://localhost:3000`

## Testing and Validation

### Smoke Tests

Two automated smoke test scripts validate the deployment:

#### 1. Lambda IDP Smoke Test

**Script**: `infrastructure/scripts/idp_smoke.sh`

**Command**:

```bash
cd infrastructure/scripts
./idp_smoke.sh \
  -a "$(cd ../terraform && tofu output -raw idp_a_function_url)" \
  -b "$(cd ../terraform && tofu output -raw idp_b_function_url)"
```

**Tests**:

- ✅ OIDC Discovery Document (`/.well-known/openid-configuration`)
  - Validates presence of required fields: issuer, authorization_endpoint, token_endpoint, jwks_uri
- ✅ JWKS Endpoint (`/jwks`)
  - Validates keys array structure
- ✅ Authorization Endpoint (`/authorize`)
  - Validates endpoint is accessible (HTTP 200/302/400)

**Expected Result**: All tests pass with green checkmarks

#### 2. S3 Access Control Smoke Test

See [MS-004 - S3 Policy Configuration for Poison](MS-004%20-%20S3%20Policy%20Configuration%20for%20Poison.md) for S3 access control testing and validation.

### Manual Validation

#### Cognito Hosted UI

**Access URL**:

```bash
cd infrastructure/terraform
tofu output cognito_hosted_ui_url
```

**Validation Steps**:

1. Visit the Hosted UI URL
2. Verify only "IDP-A" and "IDP-B" buttons are displayed
3. Verify no username/password fields are present
4. Click IDP-A or IDP-B to initiate federated login

**Expected Behavior**: Redirects to Lambda IDP authorization endpoint

## Deployment

### Prerequisites

- AWS CLI configured with credentials
- OpenTofu/Terraform >= 1.0
- Node.js 20.x or 22.x (for local IDP development)
- jq (for smoke test JSON parsing)

### Deployment Commands

```bash
# Navigate to Terraform directory
cd infrastructure/terraform

# Initialize Terraform
tofu init

# Plan deployment
tofu plan

# Apply configuration
tofu apply

# View outputs
tofu output
```

### Outputs

After successful deployment, the following outputs are available:

```bash
# Lambda IDP Function URLs
tofu output idp_a_function_url
tofu output idp_b_function_url

# Cognito
tofu output cognito_user_pool_id
tofu output cognito_app_client_id
tofu output cognito_hosted_ui_url
```

**Note**: S3 outputs are documented in [MS-004](MS-004%20-%20S3%20Policy%20Configuration%20for%20Poison.md)

## Lessons Learned

### Lambda Function URLs

- Lambda Function URLs require explicit IAM permissions with specific conditions
- Condition `lambda:FunctionUrlAuthType = "NONE"` required for public access
- Express apps need `serverless-http` wrapper for Lambda compatibility

### Cognito OIDC Providers

- `attributes_request_method` is required but not obvious in error messages
- Use `GET` for attributes request method with Lambda Function URLs
- Disabling native auth requires setting `allow_admin_create_user_only = true`

### Modular Terraform

- Reusable modules significantly improve maintainability
- Clear variable definitions and outputs essential for module composition
- Module dependencies managed via explicit `depends_on` clauses

**Note**: S3 bucket policy lessons learned are documented in [MS-004](MS-004%20-%20S3%20Policy%20Configuration%20for%20Poison.md)

## Next Steps

- [ ] Implement backend API for approval workflow (MS-005)
- [ ] Configure Step Functions for approval process (MS-005)
- [ ] Integrate Cognito authorizer with API Gateway (MS-007)
- [ ] Build SPA frontend for login and request workflow (MS-009)
