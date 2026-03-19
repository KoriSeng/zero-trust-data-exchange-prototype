locals {
  user_pool_name = coalesce(var.user_pool_name, "${var.project_name}-${var.environment}")
  base_tags = merge({
    Name        = local.user_pool_name
    Environment = var.environment
    Project     = var.project_name
  }, var.tags)
}

# Cognito User Pool
resource "aws_cognito_user_pool" "main" {
  name = local.user_pool_name

  # Allow users to sign in with email (from federated IdP only)
  username_attributes      = ["email"]
  auto_verified_attributes = ["email"]

  # Disable self-registration - only federated IdP users allowed
  admin_create_user_config {
    allow_admin_create_user_only = true
  }

  tags = local.base_tags
}

# Cognito User Pool Domain (for hosted UI)
resource "aws_cognito_user_pool_domain" "main" {
  domain       = "${var.project_name}-${var.environment}-${substr(aws_cognito_user_pool.main.id, 0, 8)}"
  user_pool_id = aws_cognito_user_pool.main.id
}

# OIDC Identity Providers
resource "aws_cognito_identity_provider" "oidc" {
  for_each = { for idp in var.idp_providers : idp.provider_name => idp }

  user_pool_id  = aws_cognito_user_pool.main.id
  provider_name = each.value.provider_name
  provider_type = each.value.provider_type

  provider_details = {
    authorize_scopes          = "openid email profile"
    client_id                 = each.value.client_id
    client_secret             = each.value.client_secret
    oidc_issuer               = trimsuffix(each.value.issuer_url, "/")
    authorize_url             = "${trimsuffix(each.value.issuer_url, "/")}/authorize"
    token_url                 = "${trimsuffix(each.value.issuer_url, "/")}/token"
    jwks_uri                  = "${trimsuffix(each.value.issuer_url, "/")}/jwks"
    attributes_url            = "${trimsuffix(each.value.issuer_url, "/")}/userinfo"
    attributes_request_method = "GET"
  }

  attribute_mapping = {
    email    = "email"
    username = "sub"
    name     = "name"
  }
}

# App Client
resource "aws_cognito_user_pool_client" "main" {
  name         = "${local.user_pool_name}-client"
  user_pool_id = aws_cognito_user_pool.main.id

  # Public SPA client (no client secret) so browser OAuth code exchange can succeed.
  generate_secret = false

  # OAuth configuration
  allowed_oauth_flows_user_pool_client = true
  allowed_oauth_flows                  = ["code"]
  allowed_oauth_scopes                 = ["openid", "email", "profile"]

  callback_urls = var.callback_urls
  logout_urls   = var.logout_urls

  # Support OIDC identity providers only (COGNITO removed to force federated login)
  supported_identity_providers = [
    for idp in aws_cognito_identity_provider.oidc : idp.provider_name
  ]

  # Disable username/password auth flows - only allow OAuth through identity providers
  explicit_auth_flows = []

  # Token validity
  access_token_validity  = 60 # minutes
  id_token_validity      = 60 # minutes
  refresh_token_validity = 30 # days

  token_validity_units {
    access_token  = "minutes"
    id_token      = "minutes"
    refresh_token = "days"
  }

  depends_on = [aws_cognito_identity_provider.oidc]
}
