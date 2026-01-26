variable "project_name" {
  description = "Project name for resource naming"
  type        = string
}

variable "environment" {
  description = "Environment name"
  type        = string
}

variable "user_pool_name" {
  description = "Optional explicit Cognito User Pool name"
  type        = string
  default     = null
}

variable "idp_providers" {
  description = "List of OIDC identity providers to configure"
  type = list(object({
    provider_name = string
    provider_type = string
    issuer_url    = string
    client_id     = string
    client_secret = string
  }))
  default = []
}

variable "callback_urls" {
  description = "Allowed callback URLs for the app client"
  type        = list(string)
  default     = ["http://localhost:3000/callback"]
}

variable "logout_urls" {
  description = "Allowed logout URLs for the app client"
  type        = list(string)
  default     = ["http://localhost:3000"]
}

variable "tags" {
  description = "Additional tags to apply"
  type        = map(string)
  default     = {}
}
