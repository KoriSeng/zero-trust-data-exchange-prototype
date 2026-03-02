variable "project_name" {
  description = "Project name used for resource naming"
  type        = string
}

variable "environment" {
  description = "Deployment environment (dev, staging, prod)"
  type        = string
  default     = "dev"
}

variable "aws_region" {
  description = "AWS region"
  type        = string
  default     = "us-east-1"
}

# ── Cognito (from cognito module outputs) ─────────────────────────────────────

variable "cognito_user_pool_id" {
  description = "Cognito User Pool ID used for JWT authorizer"
  type        = string
}

variable "cognito_user_pool_endpoint" {
  description = "Cognito User Pool endpoint (e.g. cognito-idp.us-east-1.amazonaws.com/us-east-1_xxxxx)"
  type        = string
}

variable "cognito_app_client_id" {
  description = "Cognito App Client ID for JWT audience validation"
  type        = string
}

# ── S3 ────────────────────────────────────────────────────────────────────────

variable "s3_data_bucket" {
  description = "Name of the S3 bucket holding dataset objects"
  type        = string
  default     = "zero-trust-data"
}

variable "s3_requests_bucket" {
  description = "Name of the S3 bucket for request metadata / audit trail"
  type        = string
  default     = "zero-trust-requests"
}

# ── Step Functions ────────────────────────────────────────────────────────────

variable "step_functions_approval_arn" {
  description = "ARN of the Step Functions state machine for approval workflow (optional)"
  type        = string
  default     = ""
}

# ── DocumentDB ───────────────────────────────────────────────────────────────

variable "docdb_master_username" {
  description = "DocumentDB master username"
  type        = string
  default     = "ztadmin"
}

variable "docdb_master_password" {
  description = "DocumentDB master password (sensitive)"
  type        = string
  sensitive   = true
}

variable "docdb_instance_class" {
  description = "DocumentDB instance class"
  type        = string
  default     = "db.t4g.medium"
}

# ── Lambda ────────────────────────────────────────────────────────────────────

variable "lambda_memory_mb" {
  description = "Lambda function memory in MB"
  type        = number
  default     = 512
}

variable "lambda_timeout_s" {
  description = "Lambda function timeout in seconds"
  type        = number
  default     = 30
}
