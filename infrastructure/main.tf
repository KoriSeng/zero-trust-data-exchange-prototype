# Zero Trust Data Exchange - Terraform Configuration
# This is a placeholder/example configuration to demonstrate Infrastructure as Code
# Actual resources should be defined based on project requirements

terraform {
  required_version = ">= 1.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
    archive = {
      source  = "hashicorp/archive"
      version = "~> 2.0"
    }
  }

  backend "s3" {}
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project     = "ZeroTrustDataExchange"
      Environment = var.environment
      ManagedBy   = "Terraform"
      Purpose     = "Academic-Prototype"
      Requirement = "REQ-013" # Traceability to Infrastructure as Code requirement
    }
  }
}

# Variables
variable "aws_region" {
  description = "AWS region for resource deployment"
  type        = string
  default     = "us-east-1"
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  default     = "dev"
}

variable "project_name" {
  description = "Project name for resource naming"
  type        = string
  default     = "zero-trust-prototype"
}

variable "docdb_master_password" {
  description = "DocumentDB master password for the backend database"
  type        = string
  sensitive   = true
  default     = "" # Set in terraform.tfvars or via TF_VAR_docdb_master_password
}

# Data source to get current AWS account ID
data "aws_caller_identity" "current" {}

# ============================================================================
# IAM Role for Lambda Execution
# ============================================================================

resource "aws_iam_role" "lambda_exec_role" {
  name = "${var.project_name}-lambda-exec-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "lambda.amazonaws.com"
        }
      }
    ]
  })
}

# Attach basic Lambda execution policy
resource "aws_iam_role_policy_attachment" "lambda_basic_execution" {
  role       = aws_iam_role.lambda_exec_role.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

# ============================================================================
# OIDC IDP Modules
# ============================================================================

module "idp_a" {
  source = "./modules/oidc_idp"

  idp_name             = "a"
  idp_display_name     = "Issuer A"
  idp_user_set         = "A"
  project_name         = var.project_name
  source_directory     = "${path.module}/../implementation/idp-a"
  lambda_exec_role_arn = aws_iam_role.lambda_exec_role.arn

  lambda_runtime     = "nodejs22.x"
  lambda_timeout     = 30
  lambda_memory_size = 512

  tags = {
    Name   = "OIDC-IDP-A"
    Issuer = "A"
  }

  depends_on = [aws_iam_role_policy_attachment.lambda_basic_execution]
}

module "idp_b" {
  source = "./modules/oidc_idp"

  idp_name             = "b"
  idp_display_name     = "Issuer B"
  idp_user_set         = "B"
  project_name         = var.project_name
  source_directory     = "${path.module}/../implementation/idp-b"
  lambda_exec_role_arn = aws_iam_role.lambda_exec_role.arn

  lambda_runtime     = "nodejs22.x"
  lambda_timeout     = 30
  lambda_memory_size = 512

  tags = {
    Name   = "OIDC-IDP-B"
    Issuer = "B"
  }

  depends_on = [aws_iam_role_policy_attachment.lambda_basic_execution]
}

module "s3_poc" {
  source = "./modules/s3_poc"

  project_name = var.project_name
  environment  = var.environment

  allow_account_id = data.aws_caller_identity.current.account_id
  exempt_user_arn  = data.aws_caller_identity.current.arn
}

# ============================================================================
# Cognito User Pool with OIDC Identity Providers
# ============================================================================

module "cognito" {
  source = "./modules/cognito"

  project_name = var.project_name
  environment  = var.environment

  idp_providers = [
    {
      provider_name = "IDP-A"
      provider_type = "OIDC"
      issuer_url    = module.idp_a.function_url
      client_id     = "cognito-client-a"
      client_secret = "cognito-secret-a-change-me"
    },
    {
      provider_name = "IDP-B"
      provider_type = "OIDC"
      issuer_url    = module.idp_b.function_url
      issuer_url    = module.idp_b.function_url
      client_id     = "cognito-client-b"
      client_secret = "cognito-secret-b-change-me"
    }
  ]

  callback_urls = [
    "http://localhost:3000/callback",
    "https://localhost:3000/callback"
  ]

  logout_urls = [
    "http://localhost:3000",
    "https://localhost:3000"
  ]

  depends_on = [module.idp_a, module.idp_b]
}

# ============================================================================
# Backend Lambda + API Gateway + DocumentDB
# ============================================================================

module "backend_lambda" {
  source = "./modules/backend_lambda"

  project_name = var.project_name
  environment  = var.environment
  aws_region   = var.aws_region

  cognito_user_pool_id       = module.cognito.user_pool_id
  cognito_user_pool_endpoint = module.cognito.user_pool_endpoint
  cognito_app_client_id      = module.cognito.app_client_id

  docdb_master_password = var.docdb_master_password

  depends_on = [module.cognito]
}

# ============================================================================
# Outputs
# ============================================================================

output "environment" {
  description = "Deployment environment"
  value       = var.environment
}

output "aws_region" {
  description = "AWS region"
  value       = var.aws_region
}

output "idp_a_function_name" {
  description = "Name of IDP-A Lambda function"
  value       = module.idp_a.lambda_function_name
}

output "idp_a_function_url" {
  description = "Function URL for IDP-A"
  value       = module.idp_a.function_url
}

output "idp_b_function_name" {
  description = "Name of IDP-B Lambda function"
  value       = module.idp_b.lambda_function_name
}

output "idp_b_function_url" {
  description = "Function URL for IDP-B"
  value       = module.idp_b.function_url
}

output "poc_bucket_name" {
  description = "POC data bucket name"
  value       = module.s3_poc.bucket_name
}

output "poc_bucket_arn" {
  description = "POC data bucket ARN"
  value       = module.s3_poc.bucket_arn
}

output "poc_presigner_role_arn" {
  description = "IAM role ARN used to generate pre-signed URLs for the POC bucket"
  value       = module.s3_poc.presigner_role_arn
}

output "cognito_user_pool_id" {
  description = "Cognito User Pool ID"
  value       = module.cognito.user_pool_id
}

output "cognito_app_client_id" {
  description = "Cognito App Client ID"
  value       = module.cognito.app_client_id
}

output "cognito_hosted_ui_url" {
  description = "Cognito Hosted UI URL"
  value       = module.cognito.hosted_ui_url
}

output "backend_api_url" {
  description = "Backend API Gateway invoke URL"
  value       = module.backend_lambda.api_gateway_url
}

output "backend_docdb_endpoint" {
  description = "DocumentDB cluster endpoint (internal, for debugging)"
  value       = module.backend_lambda.docdb_endpoint
}
