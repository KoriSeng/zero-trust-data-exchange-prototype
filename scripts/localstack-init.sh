#!/bin/bash
# Zero Trust Data Exchange — LocalStack Pro initialisation
# Runs inside the LocalStack container once it is fully ready.
# Mounted at: /etc/localstack/init/ready.d/01-init.sh
# All operations are idempotent — safe to re-run on container restart (PERSISTENCE=1).

set -e

AWS_REGION="${AWS_DEFAULT_REGION:-us-east-1}"
DATA_BUCKET="zero-trust-data"
REQUESTS_BUCKET="zero-trust-requests"
COGNITO_DOMAIN_PREFIX="zero-trust-local"
CONFIG_FILE="/var/lib/localstack/cognito-config.env"

echo "=== LocalStack Init: Zero Trust Data Exchange ==="

# ── S3 ─────────────────────────────────────────────────────────────────────────
echo "[S3] Creating buckets..."
awslocal s3 mb "s3://${DATA_BUCKET}" --region "$AWS_REGION" 2>/dev/null \
  || echo "  s3://${DATA_BUCKET} already exists"
awslocal s3 mb "s3://${REQUESTS_BUCKET}" --region "$AWS_REGION" 2>/dev/null \
  || echo "  s3://${REQUESTS_BUCKET} already exists"

echo "[S3] Seeding sample data..."
awslocal s3 cp - "s3://${DATA_BUCKET}/sample-data-001.csv" 2>/dev/null <<'CSVEOF' || true
id,name,email,department
001,Alice Smith,alice@example.com,Engineering
002,Bob Johnson,bob@example.com,Sales
003,Carol Williams,carol@example.com,Marketing
CSVEOF

# ── IAM ────────────────────────────────────────────────────────────────────────
echo "[IAM] Creating Step Functions execution role..."
awslocal iam create-role \
  --role-name StepFunctionsRole \
  --assume-role-policy-document '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":"states.amazonaws.com"},"Action":"sts:AssumeRole"}]}' \
  2>/dev/null || echo "  Role StepFunctionsRole already exists"

# ── Step Functions ──────────────────────────────────────────────────────────────
echo "[SFN] Creating approval workflow state machine..."
awslocal stepfunctions create-state-machine \
  --name "ZeroTrustApprovalWorkflow" \
  --definition '{
    "Comment":"Data Access Request Approval Workflow",
    "StartAt":"WaitForApproval",
    "States":{
      "WaitForApproval":{"Type":"Wait","Seconds":300,"Next":"CheckApprovalStatus"},
      "CheckApprovalStatus":{"Type":"Pass","Result":"APPROVED","ResultPath":"$.approval_status","Next":"IsApproved"},
      "IsApproved":{"Type":"Choice","Choices":[{"Variable":"$.approval_status","StringEquals":"APPROVED","Next":"ProvisionAccess"}],"Default":"RequestDenied"},
      "ProvisionAccess":{"Type":"Pass","Result":"Access provisioned","ResultPath":"$.result","Next":"Success"},
      "RequestDenied":{"Type":"Pass","Result":"Request denied","ResultPath":"$.result","Next":"Failure"},
      "Success":{"Type":"Succeed"},
      "Failure":{"Type":"Fail","Error":"RequestDenied","Cause":"Data access request was not approved"}
    }
  }' \
  --role-arn "arn:aws:iam::000000000000:role/StepFunctionsRole" \
  --region "$AWS_REGION" \
  2>/dev/null || echo "  State machine ZeroTrustApprovalWorkflow already exists"

# ── Cognito ─────────────────────────────────────────────────────────────────────
echo "[Cognito] Setting up User Pool, domain, and federated IDPs..."

# Reuse existing pool if present (PERSISTENCE=1 keeps state across restarts)
EXISTING_POOL=$(awslocal cognito-idp list-user-pools --max-results 60 --region "$AWS_REGION" \
  --query 'UserPools[?Name==`zero-trust-local`].Id | [0]' --output text 2>/dev/null || echo "")

if [ -n "$EXISTING_POOL" ] && [ "$EXISTING_POOL" != "None" ]; then
  USER_POOL_ID="$EXISTING_POOL"
  echo "  User Pool already exists: $USER_POOL_ID"
else
  USER_POOL_ID=$(awslocal cognito-idp create-user-pool \
    --pool-name zero-trust-local \
    --region "$AWS_REGION" \
    --query 'UserPool.Id' --output text)
  echo "  Created User Pool: $USER_POOL_ID"
fi

# Hosted UI domain — LocalStack uses an S3 bucket named after the domain prefix to serve assets
awslocal s3 mb "s3://${COGNITO_DOMAIN_PREFIX}" --region "$AWS_REGION" 2>/dev/null \
  || echo "  s3://${COGNITO_DOMAIN_PREFIX} already exists"

awslocal cognito-idp create-user-pool-domain \
  --user-pool-id "$USER_POOL_ID" \
  --domain "$COGNITO_DOMAIN_PREFIX" \
  --region "$AWS_REGION" \
  2>/dev/null || echo "  Domain ${COGNITO_DOMAIN_PREFIX} already exists"

# IDP-A — OIDC federated identity provider
# host.docker.internal routes from the LocalStack container → host machine → idp-a container
IDP_A_URL="${IDP_A_ISSUER_URL:-http://host.docker.internal:9001}"
echo "[Cognito] Creating IDP-A..."
awslocal cognito-idp create-identity-provider \
  --user-pool-id "$USER_POOL_ID" \
  --provider-name IDP-A \
  --provider-type OIDC \
  --provider-details "{
    \"oidc_issuer\":\"${IDP_A_URL}\",
    \"authorize_url\":\"${IDP_A_URL}/authorize\",
    \"token_url\":\"${IDP_A_URL}/token\",
    \"jwks_uri\":\"${IDP_A_URL}/jwks\",
    \"client_id\":\"cognito-local\",
    \"client_secret\":\"cognito-local-secret\",
    \"authorize_scopes\":\"openid profile email\"
  }" \
  --attribute-mapping '{"username":"sub","email":"email","name":"name"}' \
  --region "$AWS_REGION" 2>&1 | grep -v "already exists" || true

# IDP-B — OIDC federated identity provider
IDP_B_URL="${IDP_B_ISSUER_URL:-http://host.docker.internal:9002}"
echo "[Cognito] Creating IDP-B..."
awslocal cognito-idp create-identity-provider \
  --user-pool-id "$USER_POOL_ID" \
  --provider-name IDP-B \
  --provider-type OIDC \
  --provider-details "{
    \"oidc_issuer\":\"${IDP_B_URL}\",
    \"authorize_url\":\"${IDP_B_URL}/authorize\",
    \"token_url\":\"${IDP_B_URL}/token\",
    \"jwks_uri\":\"${IDP_B_URL}/jwks\",
    \"client_id\":\"cognito-local\",
    \"client_secret\":\"cognito-local-secret\",
    \"authorize_scopes\":\"openid profile email\"
  }" \
  --attribute-mapping '{"username":"sub","email":"email","name":"name"}' \
  --region "$AWS_REGION" 2>&1 | grep -v "already exists" || true

# Verify IDPs exist
echo "[Cognito] Verifying IDPs..."
awslocal cognito-idp list-identity-providers \
  --user-pool-id "$USER_POOL_ID" \
  --region "$AWS_REGION" \
  --query 'Providers[*].ProviderName' \
  --output text

# App Client
echo "[Cognito] Setting up App Client..."
EXISTING_CLIENT=$(awslocal cognito-idp list-user-pool-clients \
  --user-pool-id "$USER_POOL_ID" \
  --region "$AWS_REGION" \
  --query 'UserPoolClients[?ClientName==`zero-trust-spa`].ClientId | [0]' --output text 2>/dev/null || echo "")

if [ -n "$EXISTING_CLIENT" ] && [ "$EXISTING_CLIENT" != "None" ]; then
  CLIENT_ID="$EXISTING_CLIENT"
  echo "  App Client already exists: $CLIENT_ID"
else
  CLIENT_ID=$(awslocal cognito-idp create-user-pool-client \
    --user-pool-id "$USER_POOL_ID" \
    --client-name zero-trust-spa \
    --no-generate-secret \
    --supported-identity-providers IDP-A IDP-B \
    --callback-urls '["http://localhost:5173/callback","http://localhost:4173/callback"]' \
    --logout-urls '["http://localhost:5173","http://localhost:4173"]' \
    --allowed-o-auth-flows code \
    --allowed-o-auth-scopes openid email profile \
    --allowed-o-auth-flows-user-pool-client \
    --prevent-user-existence-errors ENABLED \
    --region "$AWS_REGION" \
    --query 'UserPoolClient.ClientId' --output text)
  if [ -z "$CLIENT_ID" ] || [ "$CLIENT_ID" = "None" ]; then
    echo "  ERROR: Failed to create App Client"
    CLIENT_ID=$(awslocal cognito-idp create-user-pool-client \
      --user-pool-id "$USER_POOL_ID" \
      --client-name zero-trust-spa \
      --no-generate-secret \
      --callback-urls '["http://localhost:5173/callback","http://localhost:4173/callback"]' \
      --logout-urls '["http://localhost:5173","http://localhost:4173"]' \
      --allowed-o-auth-flows code \
      --allowed-o-auth-scopes openid email profile \
      --allowed-o-auth-flows-user-pool-client \
      --prevent-user-existence-errors ENABLED \
      --region "$AWS_REGION" \
      --query 'UserPoolClient.ClientId' --output text)
  fi
  echo "  Created App Client: $CLIENT_ID"
fi

# Verify app client has IDPs configured
echo "[Cognito] Verifying App Client configuration..."
awslocal cognito-idp describe-user-pool-client \
  --user-pool-id "$USER_POOL_ID" \
  --client-id "$CLIENT_ID" \
  --region "$AWS_REGION" \
  --query 'UserPoolClient.SupportedIdentityProviders' \
  --output text || true

# Write config to the shared volume so the SPA container and generate-spa-env.sh can read it.
# Use the Cognito user-pool domain form to avoid LocalStack Hosted UI asset lookup under bucket "_aws".
cat > "$CONFIG_FILE" <<EOF
USER_POOL_ID=${USER_POOL_ID}
CLIENT_ID=${CLIENT_ID}
COGNITO_DOMAIN=${COGNITO_DOMAIN_PREFIX}.auth.${AWS_REGION}.localhost.localstack.cloud:4566
AWS_REGION=${AWS_REGION}
EOF
echo "  Config written to ${CONFIG_FILE}"

# ── Done ───────────────────────────────────────────────────────────────────────
echo ""
echo "=== LocalStack ready ==="
echo "  S3          http://localhost:4566  (buckets: ${DATA_BUCKET}, ${REQUESTS_BUCKET})"
echo "  Step Fns    http://localhost:4566  (ZeroTrustApprovalWorkflow)"
echo "  Cognito     https://${COGNITO_DOMAIN_PREFIX}.auth.${AWS_REGION}.localhost.localstack.cloud:4566"
echo "  User Pool   ${USER_POOL_ID}"
echo "  App Client  ${CLIENT_ID}"
echo ""
echo "  Run ./scripts/generate-spa-env.sh to configure the SPA."
