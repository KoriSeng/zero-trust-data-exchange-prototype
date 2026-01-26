variable "idp_name" {
  description = "Name identifier for the IDP (e.g., 'a', 'b')"
  type        = string
}

variable "idp_display_name" {
  description = "Display name for the IDP (e.g., 'Issuer A', 'Issuer B')"
  type        = string
}

variable "idp_user_set" {
  description = "User set for the IDP (e.g., 'A', 'B')"
  type        = string
}

variable "project_name" {
  description = "Project name for resource naming"
  type        = string
}

variable "source_directory" {
  description = "Path to the IDP source code directory"
  type        = string
}

variable "lambda_exec_role_arn" {
  description = "ARN of the Lambda execution role"
  type        = string
}

variable "lambda_runtime" {
  description = "Lambda runtime"
  type        = string
  default     = "nodejs.20.x"
}

variable "lambda_timeout" {
  description = "Lambda function timeout in seconds"
  type        = number
  default     = 30
}

variable "lambda_memory_size" {
  description = "Lambda function memory size in MB"
  type        = number
  default     = 512
}

variable "cors_origins" {
  description = "CORS allowed origins for Function URL"
  type        = list(string)
  default     = ["*"]
}

variable "cors_methods" {
  description = "CORS allowed methods for Function URL"
  type        = list(string)
  default     = ["*"]
}

variable "cors_headers" {
  description = "CORS allowed headers for Function URL"
  type        = list(string)
  default     = ["content-type"]
}

variable "authorization_type" {
  description = "Authorization type for Function URL (NONE or AWS_IAM)"
  type        = string
  default     = "NONE"
}

variable "tags" {
  description = "Additional tags for resources"
  type        = map(string)
  default     = {}
}
