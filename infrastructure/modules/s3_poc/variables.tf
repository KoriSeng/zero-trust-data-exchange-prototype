variable "project_name" {
  description = "Project name for bucket naming"
  type        = string
}

variable "environment" {
  description = "Environment name for bucket naming"
  type        = string
}

variable "bucket_name" {
  description = "Optional explicit bucket name. If not set, a default name is derived."
  type        = string
  default     = null
}

variable "force_destroy" {
  description = "Force destroy the bucket (including objects) on destroy"
  type        = bool
  default     = true
}

variable "tags" {
  description = "Additional tags to apply"
  type        = map(string)
  default     = {}
}

variable "create_seed_objects" {
  description = "Whether to create seed objects for smoke testing"
  type        = bool
  default     = true
}

variable "allow_account_id" {
  description = "If set, exempt this AWS account from the poison deny (so deployer/owner can fetch tagged objects)"
  type        = string
  default     = null
}

variable "create_presigner_role" {
  description = "Whether to create an IAM role for generating pre-signed URLs"
  type        = bool
  default     = true
}

variable "presigner_role_name" {
  description = "Override IAM role name for presigner role"
  type        = string
  default     = null
}

variable "exempt_user_arn" {
  description = "Optional IAM user ARN to exempt from poison deny (for testing/debugging)"
  type        = string
  default     = null
}
