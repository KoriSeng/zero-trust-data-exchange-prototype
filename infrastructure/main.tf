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

variable "step_functions_approval_arn" {
  description = "ARN of the Step Functions state machine for approval workflow (optional)"
  type        = string
  default     = ""
}

variable "enable_logs_kms_cmk" {
  description = "Create a shared KMS CMK for CloudWatch Logs encryption"
  type        = bool
  default     = true
}

variable "enable_sfn_execution_logging" {
  description = "Enable Step Functions execution logging to CloudWatch Logs for in-stack state machine"
  type        = bool
  default     = true
}

variable "sfn_include_execution_data" {
  description = "Include execution input/output data in Step Functions logs"
  type        = bool
  default     = true
}

variable "enable_s3_data_events_cloudtrail" {
  description = "Enable CloudTrail data events for the PoC S3 bucket and deliver them to CloudWatch Logs"
  type        = bool
  default     = true
}

# Data source to get current AWS account ID
data "aws_caller_identity" "current" {}

locals {
  logs_kms_key_arn = var.enable_logs_kms_cmk ? aws_kms_key.logs[0].arn : ""
  resolved_sfn_approval_arn = (
    var.step_functions_approval_arn != ""
    ? var.step_functions_approval_arn
    : aws_sfn_state_machine.approval[0].arn
  )
  backend_api_base_url = module.backend_ecs.api_gateway_url
}

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
# KMS — Shared CMK for CloudWatch Logs encryption
# ============================================================================

resource "aws_kms_key" "logs" {
  count = var.enable_logs_kms_cmk ? 1 : 0

  description             = "${var.project_name} shared key for CloudWatch Logs encryption"
  deletion_window_in_days = 7
  enable_key_rotation     = true

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "EnableRootAccess"
        Effect = "Allow"
        Principal = {
          AWS = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:root"
        }
        Action   = "kms:*"
        Resource = "*"
      },
      {
        Sid    = "AllowCloudWatchLogsEncryption"
        Effect = "Allow"
        Principal = {
          Service = "logs.${var.aws_region}.amazonaws.com"
        }
        Action = [
          "kms:Encrypt",
          "kms:Decrypt",
          "kms:ReEncrypt*",
          "kms:GenerateDataKey*",
          "kms:DescribeKey"
        ]
        Resource = "*"
        Condition = {
          ArnLike = {
            "kms:EncryptionContext:aws:logs:arn" = "arn:aws:logs:${var.aws_region}:${data.aws_caller_identity.current.account_id}:*"
          }
        }
      }
    ]
  })

  tags = {
    Name = "${var.project_name}-logs-cmk"
  }
}

resource "aws_kms_alias" "logs" {
  count = var.enable_logs_kms_cmk ? 1 : 0

  name          = "alias/${var.project_name}-logs"
  target_key_id = aws_kms_key.logs[0].key_id
}

resource "aws_iam_role_policy" "lambda_exec_kms_logs" {
  count = var.enable_logs_kms_cmk ? 1 : 0

  name = "${var.project_name}-lambda-exec-kms-logs"
  role = aws_iam_role.lambda_exec_role.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Sid    = "AllowKMSForCloudWatchLogs"
      Effect = "Allow"
      Action = [
        "kms:GenerateDataKey*",
        "kms:Decrypt",
        "kms:DescribeKey"
      ]
      Resource = aws_kms_key.logs[0].arn
    }]
  })
}

# ============================================================================
# Step Functions — Approval workflow
# ============================================================================

resource "aws_iam_role" "stepfunctions_exec" {
  count = var.step_functions_approval_arn == "" ? 1 : 0

  name = "${var.project_name}-${var.environment}-sfn-exec-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Principal = {
        Service = "states.amazonaws.com"
      }
      Action = "sts:AssumeRole"
    }]
  })
}

resource "aws_sqs_queue" "approval_claim_callbacks" {
  count = var.step_functions_approval_arn == "" ? 1 : 0

  name                      = "${var.project_name}-${var.environment}-approval-claim-callbacks"
  message_retention_seconds = 1209600
}

resource "aws_sqs_queue" "approval_decision_callbacks" {
  count = var.step_functions_approval_arn == "" ? 1 : 0

  name                      = "${var.project_name}-${var.environment}-approval-decision-callbacks"
  message_retention_seconds = 1209600
}

resource "aws_sqs_queue" "approval_otp_dispatch" {
  count = var.step_functions_approval_arn == "" ? 1 : 0

  name                      = "${var.project_name}-${var.environment}-approval-otp-dispatch"
  message_retention_seconds = 1209600
}

resource "aws_sqs_queue" "approval_claim_timeouts" {
  count = var.step_functions_approval_arn == "" ? 1 : 0

  name                      = "${var.project_name}-${var.environment}-approval-claim-timeouts"
  message_retention_seconds = 1209600
}

resource "aws_iam_role_policy" "stepfunctions_claim_callbacks" {
  count = var.step_functions_approval_arn == "" ? 1 : 0

  name = "${var.project_name}-${var.environment}-sfn-claim-callbacks"
  role = aws_iam_role.stepfunctions_exec[0].id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = ["sqs:SendMessage"]
        Resource = [
          aws_sqs_queue.approval_decision_callbacks[0].arn,
          aws_sqs_queue.approval_otp_dispatch[0].arn,
          aws_sqs_queue.approval_claim_callbacks[0].arn,
          aws_sqs_queue.approval_claim_timeouts[0].arn
        ]
      },
      {
        Effect = "Allow"
        Action = [
          "logs:CreateLogDelivery",
          "logs:GetLogDelivery",
          "logs:UpdateLogDelivery",
          "logs:DeleteLogDelivery",
          "logs:ListLogDeliveries",
          "logs:PutResourcePolicy",
          "logs:DescribeResourcePolicies",
          "logs:DescribeLogGroups"
        ]
        Resource = "*"
      }
    ]
  })
}

resource "aws_cloudwatch_log_group" "sfn_execution" {
  count = var.step_functions_approval_arn == "" && var.enable_sfn_execution_logging ? 1 : 0

  name              = "/aws/vendedlogs/states/${var.project_name}-${var.environment}-approval-workflow"
  retention_in_days = 30
  kms_key_id        = local.logs_kms_key_arn != "" ? local.logs_kms_key_arn : null
}

resource "aws_sfn_state_machine" "approval" {
  count = var.step_functions_approval_arn == "" ? 1 : 0

  name     = "${var.project_name}-${var.environment}-approval-workflow"
  role_arn = aws_iam_role.stepfunctions_exec[0].arn

  definition = jsonencode({
    Comment = "Zero Trust approval workflow lifecycle (approval wait -> send OTP -> claim wait)"
    StartAt = "RequestCreated"
    States = {
      RequestCreated = {
        Type       = "Pass"
        ResultPath = "$.workflow"
        Result = {
          state = "REQUEST_CREATED"
        }
        Next = "ApprovalDecision"
      }
      ApprovalDecision = {
        Type     = "Task"
        Resource = "arn:aws:states:::sqs:sendMessage.waitForTaskToken"
        Parameters = {
          QueueUrl = aws_sqs_queue.approval_decision_callbacks[0].url
          MessageBody = {
            "requestId.$" = "$.request_id"
            state         = "APPROVAL_DECISION_PENDING"
            "taskToken.$" = "$$.Task.Token"
          }
        }
        ResultPath     = "$.approvalDecision"
        TimeoutSeconds = 86400
        Next           = "SendOtp"
      }
      SendOtp = {
        Type     = "Task"
        Resource = "arn:aws:states:::sqs:sendMessage"
        Parameters = {
          QueueUrl = aws_sqs_queue.approval_otp_dispatch[0].url
          MessageBody = {
            "requestId.$"  = "$.request_id"
            state          = "OTP_DISPATCH"
            "approvedAt.$" = "$$.State.EnteredTime"
          }
        }
        ResultPath = "$.otpDispatch"
        Next       = "ClaimPending"
      }
      ClaimPending = {
        Type     = "Task"
        Resource = "arn:aws:states:::sqs:sendMessage.waitForTaskToken"
        Parameters = {
          QueueUrl = aws_sqs_queue.approval_claim_callbacks[0].url
          MessageBody = {
            "requestId.$" = "$.request_id"
            state         = "CLAIM_PENDING"
            "taskToken.$" = "$$.Task.Token"
          }
        }
        TimeoutSeconds = 86400
        ResultPath     = "$.claimResult"
        Next           = "AccessWindowWait"
      }
      AccessWindowWait = {
        Type        = "Wait"
        SecondsPath = "$.approvalDecision.claim_window_seconds"
        Next        = "DisableClaim"
      }
      DisableClaim = {
        Type     = "Task"
        Resource = "arn:aws:states:::sqs:sendMessage"
        Parameters = {
          QueueUrl = aws_sqs_queue.approval_claim_timeouts[0].url
          MessageBody = {
            "requestId.$"     = "$.request_id"
            state             = "CLAIM_WINDOW_EXPIRED"
            "expiredAt.$"     = "$$.State.EnteredTime"
            "windowSeconds.$" = "$.approvalDecision.claim_window_seconds"
          }
        }
        ResultPath = "$.claimTimeoutDispatch"
        Next       = "Complete"
      }
      Complete = {
        Type = "Succeed"
      }
    }
  })

  dynamic "logging_configuration" {
    for_each = var.enable_sfn_execution_logging ? [1] : []
    content {
      level                  = "ALL"
      include_execution_data = var.sfn_include_execution_data
      log_destination        = "${aws_cloudwatch_log_group.sfn_execution[0].arn}:*"
    }
  }
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
  logs_kms_key_arn   = local.logs_kms_key_arn

  tags = {
    Name   = "OIDC-IDP-A"
    Issuer = "A"
  }

  depends_on = [
    aws_iam_role_policy_attachment.lambda_basic_execution
  ]
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
  logs_kms_key_arn   = local.logs_kms_key_arn

  tags = {
    Name   = "OIDC-IDP-B"
    Issuer = "B"
  }

  depends_on = [
    aws_iam_role_policy_attachment.lambda_basic_execution
  ]
}

module "s3_poc" {
  source = "./modules/s3_poc"

  project_name = var.project_name
  environment  = var.environment

  allow_account_id = data.aws_caller_identity.current.account_id
  exempt_user_arn  = data.aws_caller_identity.current.arn
}

resource "aws_s3_bucket" "cloudtrail_logs" {
  count = var.enable_s3_data_events_cloudtrail ? 1 : 0

  bucket = "${var.project_name}-${var.environment}-cloudtrail-logs-${data.aws_caller_identity.current.account_id}"
}

resource "aws_s3_bucket_public_access_block" "cloudtrail_logs" {
  count = var.enable_s3_data_events_cloudtrail ? 1 : 0

  bucket                  = aws_s3_bucket.cloudtrail_logs[0].id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

data "aws_iam_policy_document" "cloudtrail_bucket_policy" {
  count = var.enable_s3_data_events_cloudtrail ? 1 : 0

  statement {
    sid    = "AWSCloudTrailAclCheck"
    effect = "Allow"
    principals {
      type        = "Service"
      identifiers = ["cloudtrail.amazonaws.com"]
    }
    actions   = ["s3:GetBucketAcl"]
    resources = [aws_s3_bucket.cloudtrail_logs[0].arn]
  }

  statement {
    sid    = "AWSCloudTrailWrite"
    effect = "Allow"
    principals {
      type        = "Service"
      identifiers = ["cloudtrail.amazonaws.com"]
    }
    actions = ["s3:PutObject"]
    resources = [
      "${aws_s3_bucket.cloudtrail_logs[0].arn}/AWSLogs/${data.aws_caller_identity.current.account_id}/*"
    ]
    condition {
      test     = "StringEquals"
      variable = "s3:x-amz-acl"
      values   = ["bucket-owner-full-control"]
    }
  }
}

resource "aws_s3_bucket_policy" "cloudtrail_logs" {
  count = var.enable_s3_data_events_cloudtrail ? 1 : 0

  bucket = aws_s3_bucket.cloudtrail_logs[0].id
  policy = data.aws_iam_policy_document.cloudtrail_bucket_policy[0].json
}

resource "aws_cloudwatch_log_group" "s3_data_events" {
  count = var.enable_s3_data_events_cloudtrail ? 1 : 0

  name              = "/aws/cloudtrail/${var.project_name}-${var.environment}-s3-data-events"
  retention_in_days = 30
  kms_key_id        = local.logs_kms_key_arn != "" ? local.logs_kms_key_arn : null
}

resource "aws_iam_role" "cloudtrail_to_cwlogs" {
  count = var.enable_s3_data_events_cloudtrail ? 1 : 0

  name = "${var.project_name}-${var.environment}-cloudtrail-cwlogs-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Principal = {
        Service = "cloudtrail.amazonaws.com"
      }
      Action = "sts:AssumeRole"
    }]
  })
}

resource "aws_iam_role_policy" "cloudtrail_to_cwlogs" {
  count = var.enable_s3_data_events_cloudtrail ? 1 : 0

  name = "${var.project_name}-${var.environment}-cloudtrail-cwlogs-policy"
  role = aws_iam_role.cloudtrail_to_cwlogs[0].id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Action = [
        "logs:CreateLogStream",
        "logs:PutLogEvents"
      ]
      Resource = "${aws_cloudwatch_log_group.s3_data_events[0].arn}:*"
    }]
  })
}

resource "aws_cloudtrail" "s3_data_events" {
  count = var.enable_s3_data_events_cloudtrail ? 1 : 0

  name                          = "${var.project_name}-${var.environment}-s3-data-events"
  s3_bucket_name                = aws_s3_bucket.cloudtrail_logs[0].id
  include_global_service_events = false
  is_multi_region_trail         = false
  enable_logging                = true

  cloud_watch_logs_group_arn = "${aws_cloudwatch_log_group.s3_data_events[0].arn}:*"
  cloud_watch_logs_role_arn  = aws_iam_role.cloudtrail_to_cwlogs[0].arn

  event_selector {
    read_write_type           = "All"
    include_management_events = false

    data_resource {
      type = "AWS::S3::Object"
      values = [
        "${module.s3_poc.bucket_arn}/"
      ]
    }
  }

  depends_on = [
    aws_s3_bucket_policy.cloudtrail_logs,
    aws_iam_role_policy.cloudtrail_to_cwlogs
  ]
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
      client_id     = "cognito-client-b"
      client_secret = "cognito-secret-b-change-me"
    }
  ]

  callback_urls = [
    "https://${aws_cloudfront_distribution.spa.domain_name}/callback"
  ]

  logout_urls = [
    "https://${aws_cloudfront_distribution.spa.domain_name}"
  ]

  depends_on = [module.idp_a, module.idp_b]
}

module "backend_ecs" {
  source = "./modules/backend_ecs"

  project_name = var.project_name
  environment  = var.environment
  aws_region   = var.aws_region

  subnet_az_a = "${var.aws_region}a"
  subnet_az_b = "${var.aws_region}b"

  docdb_master_password                          = var.docdb_master_password
  docdb_master_username                          = "ztadmin"
  s3_data_bucket                                 = module.s3_poc.bucket_name
  s3_requests_bucket                             = module.s3_poc.bucket_name
  step_functions_approval_arn                    = local.resolved_sfn_approval_arn
  step_functions_approval_decision_queue_url     = var.step_functions_approval_arn == "" ? aws_sqs_queue.approval_decision_callbacks[0].url : ""
  step_functions_approval_decision_queue_arn     = var.step_functions_approval_arn == "" ? aws_sqs_queue.approval_decision_callbacks[0].arn : ""
  step_functions_approval_otp_dispatch_queue_url = var.step_functions_approval_arn == "" ? aws_sqs_queue.approval_otp_dispatch[0].url : ""
  step_functions_approval_otp_dispatch_queue_arn = var.step_functions_approval_arn == "" ? aws_sqs_queue.approval_otp_dispatch[0].arn : ""
  step_functions_claim_callback_queue_url        = var.step_functions_approval_arn == "" ? aws_sqs_queue.approval_claim_callbacks[0].url : ""
  step_functions_claim_callback_queue_arn        = var.step_functions_approval_arn == "" ? aws_sqs_queue.approval_claim_callbacks[0].arn : ""
  step_functions_claim_timeout_queue_url         = var.step_functions_approval_arn == "" ? aws_sqs_queue.approval_claim_timeouts[0].url : ""
  step_functions_claim_timeout_queue_arn         = var.step_functions_approval_arn == "" ? aws_sqs_queue.approval_claim_timeouts[0].arn : ""
  seed_database                                  = true
  seed_org_a_cognito_group_name                  = "${module.cognito.user_pool_id}_IDP-A"
  seed_org_b_cognito_group_name                  = "${module.cognito.user_pool_id}_IDP-B"
  seed_org_a_idp_issuer                          = trimsuffix(module.idp_a.function_url, "/")
  seed_org_b_idp_issuer                          = trimsuffix(module.idp_b.function_url, "/")
  cors_allowed_origins = [
    "https://${aws_cloudfront_distribution.spa.domain_name}"
  ]
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

output "approval_workflow_arn" {
  description = "Resolved Step Functions approval workflow ARN used by backend"
  value       = local.resolved_sfn_approval_arn
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
  description = "Active backend base URL (Lambda API Gateway by default, ECS ALB when enabled)"
  value       = local.backend_api_base_url
}

output "backend_api_gateway_id" {
  description = "Backend API Gateway HTTP API ID fronting ECS backend"
  value       = module.backend_ecs.api_gateway_id
}

output "backend_ecs_service_url" {
  description = "Backend ECS service URL"
  value       = module.backend_ecs.service_url
}

output "backend_ecs_cluster_name" {
  description = "Backend ECS cluster name"
  value       = module.backend_ecs.cluster_name
}

output "backend_docdb_endpoint" {
  description = "DocumentDB cluster endpoint (internal, for debugging)"
  value       = module.backend_ecs.docdb_endpoint
}

output "backend_lambda_error_alarm_name" {
  description = "CloudWatch alarm name for backend Lambda error spikes (legacy, ECS cutover active)"
  value       = null
}

output "backend_monitoring_dashboard_name" {
  description = "CloudWatch dashboard name for backend monitoring overview (legacy, ECS cutover active)"
  value       = null
}

output "logs_kms_key_arn" {
  description = "Shared KMS key ARN for CloudWatch log group encryption (null when disabled)"
  value       = local.logs_kms_key_arn != "" ? local.logs_kms_key_arn : null
}

output "sfn_execution_log_group_name" {
  description = "CloudWatch log group name for in-stack Step Functions execution logs (null when disabled or external state machine ARN is used)"
  value       = var.step_functions_approval_arn == "" && var.enable_sfn_execution_logging ? aws_cloudwatch_log_group.sfn_execution[0].name : null
}

output "s3_data_events_cloudtrail_name" {
  description = "CloudTrail trail name used for S3 data event audit logging (null when disabled)"
  value       = var.enable_s3_data_events_cloudtrail ? aws_cloudtrail.s3_data_events[0].name : null
}

output "s3_data_events_log_group_name" {
  description = "CloudWatch log group name receiving S3 data event CloudTrail logs (null when disabled)"
  value       = var.enable_s3_data_events_cloudtrail ? aws_cloudwatch_log_group.s3_data_events[0].name : null
}
