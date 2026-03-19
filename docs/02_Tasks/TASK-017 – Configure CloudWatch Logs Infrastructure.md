---
type: Task
task_id: TASK-017
title: "Configure CloudWatch Logs Infrastructure"
owner: STK-001
status: "In Progress"
related_milestone: MS-006
---

## Description

CloudWatch Logs infrastructure is partially in place. The `backend_lambda` module already provisions log groups for the backend Lambda (`/aws/lambda/${name_prefix}-backend`) and API Gateway (`/aws/apigateway/${name_prefix}-backend`), both with 30-day retention. IAM write permissions for the backend Lambda come from the `AWSLambdaVPCAccessExecutionRole` managed policy already attached to its role.

**Four gaps remain before the Definition of Done is fully met:**

1. **IDP Lambda log groups are missing.** The `oidc_idp` module creates two Lambda functions (`${project_name}-idp-a` and `${project_name}-idp-b`) with no explicit `aws_cloudwatch_log_group` resources. Without them, Lambda auto-creates log groups on first invocation with no retention policy — logs accumulate indefinitely, causing unbounded storage costs.

2. **Step Functions log group is missing.** The approval workflow state machine ARN is accepted as a variable (`step_functions_approval_arn`) but no corresponding CloudWatch log group is provisioned. Pre-provisioning it ensures a consistent 30-day retention policy and prepares the delivery target for when the state machine is wired up in Terraform.

3. **KMS encryption is not configured.** All log groups currently rely on the default AWS-managed SSE key. The Definition of Done requires a customer-managed key (CMK). For this prototype, a single shared CMK covering all log groups is the right balance between meeting the requirement and keeping infrastructure simple.

4. **KMS IAM permissions are not granted.** Once a CMK is applied to log groups, both Lambda execution roles need explicit `kms:GenerateDataKey*` / `kms:Decrypt` permissions. The CloudWatch Logs service principal also requires a grant directly in the KMS key policy — an IAM policy alone is insufficient for service-to-service encryption.

> **Architecture note — Lambda connectivity:** Lambda functions run in public VPC subnets and reach CloudWatch Logs via outbound HTTPS through the Internet Gateway. There is no NAT Gateway and no VPC Interface Endpoint for the `logs` service. This is intentional; both options add per-AZ hourly costs that are not justified for an academic prototype.

---

## Implementation Notes

Work through the four gaps in sequence — the KMS key created in step 3 is threaded back into steps 1 and 2. All snippets are ready to drop in; adjust only where a comment says so.

### 1 — IDP log groups (`modules/oidc_idp/main.tf`)

The IDP module uses `${var.project_name}-idp-${var.idp_name}` as the Lambda function name (note: no environment suffix — the module does not receive an `environment` variable). The log group name must match the function name exactly for Lambda to resolve it automatically.

Add this resource block to `modules/oidc_idp/main.tf`:

```hcl
# Explicit log group — prevents Lambda auto-creating one with no retention policy
resource "aws_cloudwatch_log_group" "idp" {
  name              = "/aws/lambda/${var.project_name}-idp-${var.idp_name}"
  retention_in_days = 30
  kms_key_id        = var.logs_kms_key_arn != "" ? var.logs_kms_key_arn : null

  tags = var.tags
}
```

Add a `depends_on` to the existing `aws_lambda_function.idp` resource so the function cannot invoke before the group exists:

```hcl
resource "aws_lambda_function" "idp" {
  # ... all existing arguments unchanged ...

  depends_on = [aws_cloudwatch_log_group.idp]
}
```

Add the new input variable to `modules/oidc_idp/variables.tf`:

```hcl
variable "logs_kms_key_arn" {
  description = "KMS key ARN used to encrypt CloudWatch log groups. Leave empty to use the AWS-managed default key."
  type        = string
  default     = ""
}
```

Pass the value from root `main.tf` into both IDP module blocks. Do this after the KMS key is created in step 3; for now you can leave it as the default empty string and fill it in once `aws_kms_key.logs` exists:

```hcl
module "idp_a" {
  # ... all existing arguments unchanged ...
  logs_kms_key_arn = aws_kms_key.logs.arn
}

module "idp_b" {
  # ... all existing arguments unchanged ...
  logs_kms_key_arn = aws_kms_key.logs.arn
}
```

---

### 2 — Step Functions log group (`modules/backend_lambda/main.tf`)

The state machine is not yet managed by Terraform (the ARN arrives as an input variable), but the log group can be pre-provisioned now. Add this alongside the two existing log groups in `modules/backend_lambda/main.tf`. The `count` guard means nothing is created when no SFN ARN is configured — useful during earlier milestone phases where Step Functions is disabled:

```hcl
# CloudWatch log group for the Step Functions approval workflow.
# Pre-provisioned with consistent retention even before the state machine
# resource is added to Terraform. Skipped when step_functions_approval_arn is empty.
resource "aws_cloudwatch_log_group" "stepfunctions" {
  count = var.step_functions_approval_arn != "" ? 1 : 0

  name              = "/aws/states/${local.name_prefix}-approval-workflow"
  retention_in_days = 30
  kms_key_id        = var.logs_kms_key_arn != "" ? var.logs_kms_key_arn : null

  tags = { Name = "${local.name_prefix}-sfn-logs" }
}
```

When you later add an `aws_sfn_state_machine` resource (TASK-014 follow-up), connect it to this group:

```hcl
logging_configuration {
  log_destination        = "${aws_cloudwatch_log_group.stepfunctions[0].arn}:*"
  include_execution_data = true
  level                  = "ERROR"
}
```

---

### 3 — Shared KMS CMK for log encryption (root `main.tf`)

Add this block to `infrastructure/main.tf`. The `data "aws_caller_identity" "current"` source already exists at line 63 — **do not add a duplicate**.

```hcl
# ============================================================================
# KMS — Shared CMK for CloudWatch Logs encryption
# A single key shared across all log groups is acceptable for a prototype.
# Cost: ~$1/month per key + $0.03 per 10,000 API calls.
# ============================================================================

resource "aws_kms_key" "logs" {
  description             = "${var.project_name} shared key for CloudWatch Logs encryption"
  deletion_window_in_days = 7
  enable_key_rotation     = true

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      # Full administrative control for the account root — ensures the key
      # can always be managed even if other policies are misconfigured.
      {
        Sid    = "EnableRootAccess"
        Effect = "Allow"
        Principal = {
          AWS = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:root"
        }
        Action   = "kms:*"
        Resource = "*"
      },
      # CloudWatch Logs service principal — MUST appear in the key policy.
      # IAM policies on Lambda roles do not cover this service-to-service path.
      # The Condition scopes the grant to log groups in this account only.
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

  tags = { Name = "${var.project_name}-logs-cmk" }
}

resource "aws_kms_alias" "logs" {
  name          = "alias/${var.project_name}-logs"
  target_key_id = aws_kms_key.logs.key_id
}
```

> **Why the key policy is required here:** CloudWatch Logs encrypts log events using its own service-linked credentials, not the Lambda function's role. The `logs.<region>.amazonaws.com` service principal must therefore be granted access directly in the KMS key policy. Granting only the Lambda role in IAM is not sufficient and will cause log delivery to silently fail. See [AWS docs — Encrypt log data in CloudWatch Logs using AWS KMS](https://docs.aws.amazon.com/AmazonCloudWatch/latest/logs/encrypt-log-data-kms.html).

---

### 4 — Wire the KMS key ARN into existing log groups (`modules/backend_lambda/`)

**Step 4a — Add the input variable** to `modules/backend_lambda/variables.tf`:

```hcl
variable "logs_kms_key_arn" {
  description = "KMS key ARN used to encrypt CloudWatch log groups. Leave empty to use the AWS-managed default key."
  type        = string
  default     = ""
}
```

**Step 4b — Update the two existing log group resources** in `modules/backend_lambda/main.tf` to add `kms_key_id`:

```hcl
resource "aws_cloudwatch_log_group" "lambda_backend" {
  name              = "/aws/lambda/${local.name_prefix}-backend"
  retention_in_days = 30
  kms_key_id        = var.logs_kms_key_arn != "" ? var.logs_kms_key_arn : null  # add this line
}

resource "aws_cloudwatch_log_group" "apigw" {
  name              = "/aws/apigateway/${local.name_prefix}-backend"
  retention_in_days = 30
  kms_key_id        = var.logs_kms_key_arn != "" ? var.logs_kms_key_arn : null  # add this line
}
```

**Step 4c — Pass the KMS key ARN** from root `main.tf` into the `backend_lambda` module:

```hcl
module "backend_lambda" {
  # ... all existing arguments unchanged ...
  logs_kms_key_arn = aws_kms_key.logs.arn
}
```

---

### 5 — KMS permissions for Lambda execution roles

Lambda functions need `kms:GenerateDataKey*` and `kms:Decrypt` to interact with KMS-encrypted log groups via the CloudWatch Logs SDK path. Add one inline policy to each role.

**IDP Lambdas — add to root `main.tf`** (the `lambda_exec_role` is defined there and shared by both IDP functions):

```hcl
resource "aws_iam_role_policy" "lambda_exec_kms_logs" {
  count = aws_kms_key.logs.arn != "" ? 1 : 0

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
      Resource = aws_kms_key.logs.arn
    }]
  })
}
```

**Backend Lambda — add to `modules/backend_lambda/main.tf`** (alongside the existing `aws_iam_role_policy` resources):

```hcl
resource "aws_iam_role_policy" "lambda_kms_logs" {
  count = var.logs_kms_key_arn != "" ? 1 : 0

  name = "${local.name_prefix}-lambda-kms-logs"
  role = aws_iam_role.lambda_backend.id

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
      Resource = var.logs_kms_key_arn
    }]
  })
}
```

---

## Scope Note

This is an academic prototype. A **single shared CMK** covering all log groups is the right tradeoff — it satisfies the encryption requirement without the per-component key overhead that would be appropriate only in a production multi-tenant system. Similarly, 30-day retention is consistent across all groups: long enough to support the validation scenarios in TASK-007, short enough to avoid runaway storage costs.

---

## Definition of Done

- [ ] CloudWatch Log Groups created for each component
  - [ ] `/aws/lambda/${project_name}-${environment}-backend` — already exists in `backend_lambda` module
  - [ ] `/aws/apigateway/${project_name}-${environment}-backend` — already exists in `backend_lambda` module
  - [ ] `/aws/lambda/${project_name}-idp-a` — new, add to `oidc_idp` module (no environment suffix; matches function naming)
  - [ ] `/aws/lambda/${project_name}-idp-b` — new, add to `oidc_idp` module (no environment suffix; matches function naming)
  - [ ] `/aws/states/${project_name}-${environment}-approval-workflow` — new, add to `backend_lambda` module (conditional on `step_functions_approval_arn`)
- [ ] Retention policies configured — 30 days on all groups, consistent with existing resources
- [ ] IAM permissions configured for log writing
  - [ ] Backend Lambda: covered by `AWSLambdaVPCAccessExecutionRole` (already attached)
  - [ ] IDP Lambdas: covered by `AWSLambdaBasicExecutionRole` (already attached via root `lambda_exec_role`)
- [ ] Log encryption enabled
  - [ ] Shared KMS CMK created in root `main.tf` with `enable_key_rotation = true`
  - [ ] KMS key policy grants `logs.${region}.amazonaws.com` encrypt/decrypt access with account-scoped condition
  - [ ] `kms_key_id` set on all `aws_cloudwatch_log_group` resources (backend Lambda, API Gateway, IDP A, IDP B, Step Functions)
  - [ ] KMS `GenerateDataKey*` / `Decrypt` inline policy added to root `lambda_exec_role` (covers IDP Lambdas)
  - [ ] KMS `GenerateDataKey*` / `Decrypt` inline policy added to `backend_lambda` module's Lambda role
- [ ] Infrastructure deployed and tested
  - [ ] `terraform plan` produces no unexpected diff after `terraform apply`
  - [ ] IDP Lambda invocation writes logs to the named group; retention policy visible in the AWS Console
  - [ ] Backend Lambda log group shows the CMK alias under **Encryption** in the AWS Console
  - [ ] `terraform destroy` cleans up all resources without errors (KMS key enters 7-day pending deletion window)
