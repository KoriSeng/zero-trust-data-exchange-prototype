variable "project_name" {
  description = "Project name used for resource naming"
  type        = string
}

variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "aws_region" {
  description = "AWS region"
  type        = string
}

variable "subnet_az_a" {
  description = "Pinned availability zone for subnet group A"
  type        = string
}

variable "subnet_az_b" {
  description = "Pinned availability zone for subnet group B"
  type        = string
}

variable "docdb_master_username" {
  description = "DocumentDB master username"
  type        = string
  default     = "ztadmin"
}

variable "docdb_master_password" {
  description = "DocumentDB master password"
  type        = string
  sensitive   = true
}

variable "s3_data_bucket" {
  description = "Dataset S3 bucket name"
  type        = string
  default     = "zero-trust-data"
}

variable "s3_requests_bucket" {
  description = "Requests S3 bucket name"
  type        = string
  default     = "zero-trust-requests"
}

variable "step_functions_approval_arn" {
  description = "Step Functions ARN (optional)"
  type        = string
  default     = ""
}

variable "cors_allowed_origins" {
  description = "Allowed CORS origins"
  type        = list(string)
}

variable "desired_count" {
  description = "ECS service desired task count"
  type        = number
  default     = 1
}

variable "cpu" {
  description = "Fargate task CPU units"
  type        = number
  default     = 256
}

variable "memory" {
  description = "Fargate task memory (MB)"
  type        = number
  default     = 512
}

variable "container_port" {
  description = "Backend container port"
  type        = number
  default     = 5000
}

variable "docdb_instance_class" {
  description = "DocumentDB instance class"
  type        = string
  default     = "db.t4g.medium"
}

variable "seed_database" {
  description = "Enable backend database seeding on startup"
  type        = bool
  default     = true
}

variable "seed_org_a_cognito_group_name" {
  description = "Seed value for Org A Cognito group name"
  type        = string
  default     = "IDP-A"
}

variable "seed_org_b_cognito_group_name" {
  description = "Seed value for Org B Cognito group name"
  type        = string
  default     = "IDP-B"
}

variable "seed_org_a_idp_issuer" {
  description = "Seed value for Org A OIDC issuer URL"
  type        = string
}

variable "seed_org_b_idp_issuer" {
  description = "Seed value for Org B OIDC issuer URL"
  type        = string
}
