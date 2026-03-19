---
type: Task
task_id: TASK-024
title: "Configure AWS SES Infrastructure"
owner: STK-001
status: "In Progress"
related_milestone: MS-008
---

## Description

Add AWS Simple Email Service (SES) Terraform resources to the existing `backend_lambda` module so that the Lambda function can send OTP and notification emails. This covers email identity verification, a configuration set for delivery tracking, an IAM inline policy granting `ses:SendEmail` to the Lambda role, and the two new environment variables the `SesService` will read at runtime.

No new Terraform module is needed — all resources live alongside the existing `aws_lambda_function.backend` in `modules/backend_lambda/main.tf`.

## Implementation Notes

### SES sandbox constraints

SES accounts start in *sandbox* mode. In sandbox mode **both** the sender *and* every recipient address must be individually verified. For the prototype, verify two specific addresses (one requester, one approver) rather than requesting production access.

To exit sandbox, submit an AWS Support ticket — this is **out of scope** for the prototype. Document the constraint in the README so the marker understands the limitation.

### 1. Verify email identities

Add two `aws_ses_email_identity` resources to `modules/backend_lambda/main.tf`. The email addresses themselves come from `terraform.tfvars` so they are not hardcoded in source:

```hcl
# ============================================================================
# SES — Email identities (sandbox: both sender and recipient must be verified)
# ============================================================================

resource "aws_ses_email_identity" "requester" {
  email = var.ses_requester_test_email   # e.g. "requester@example.com"
}

resource "aws_ses_email_identity" "approver" {
  email = var.ses_approver_test_email    # e.g. "approver@example.com"
}
```

Add the corresponding variables to `modules/backend_lambda/variables.tf`:

```hcl
variable "ses_requester_test_email" {
  description = "Test requester email verified in SES sandbox"
  type        = string
}

variable "ses_approver_test_email" {
  description = "Test approver email verified in SES sandbox"
  type        = string
}

variable "ses_from_address" {
  description = "Verified SES sender address (must match a verified identity)"
  type        = string
}
```

After `terraform apply`, AWS sends a verification email to each address. Click the link to activate the identity. The `aws_ses_email_identity` resource will show `PENDING` until the link is clicked; re-run `terraform plan` to confirm no drift.

### 2. Configuration set for delivery tracking

```hcl
resource "aws_sesv2_configuration_set" "otp" {
  configuration_set_name = "${local.name_prefix}-otp-email"

  sending_options {
    sending_enabled = true
  }

  suppression_options {
    suppressed_reasons = ["BOUNCE", "COMPLAINT"]
  }

  tags = { Name = "${local.name_prefix}-otp-email" }
}
```

The configuration set name is passed to `SesService` via the `AWS__SES__ConfigurationSet` environment variable so the Lambda can stamp every `SendEmailRequest` with it, enabling per-send delivery metrics in CloudWatch.

### 3. IAM inline policy on the Lambda role

Attach an inline policy to the existing `aws_iam_role.lambda_backend` role. Scope the `Resource` to the verified identity ARNs to follow least-privilege:

```hcl
resource "aws_iam_role_policy" "lambda_ses" {
  name = "${local.name_prefix}-lambda-ses-policy"
  role = aws_iam_role.lambda_backend.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "AllowSESSend"
        Effect = "Allow"
        Action = [
          "ses:SendEmail",
          "ses:SendRawEmail"
        ]
        Resource = [
          "arn:aws:ses:${var.aws_region}:*:identity/${var.ses_from_address}",
          "arn:aws:ses:${var.aws_region}:*:configuration-set/${local.name_prefix}-otp-email"
        ]
      }
    ]
  })
}
```

> **Note on `ses:SendRawEmail`**: the v2 `SendEmailRequest` ultimately calls the SES v2 `SendEmail` API, but the IAM action is still `ses:SendEmail`. Include `ses:SendRawEmail` as a fallback; it does not hurt and saves a round of debugging.

### 4. Lambda environment variables

Add two new keys inside the existing `environment { variables = { ... } }` block in `aws_lambda_function.backend`:

```hcl
AWS__SES__FromAddress      = var.ses_from_address
AWS__SES__ConfigurationSet = "${local.name_prefix}-otp-email"
AWS__SES__Enabled          = "true"
```

The `SesService` (TASK-025) reads these via `IConfiguration`:
- `configuration["AWS:SES:FromAddress"]`
- `configuration["AWS:SES:ConfigurationSet"]`
- `configuration.GetValue<bool>("AWS:SES:Enabled", false)` — set to `false` locally so emails are skipped during `docker-compose` development.

### 5. `terraform.tfvars` additions

```hcl
ses_from_address         = "noreply@example.com"   # must be verified in SES
ses_requester_test_email = "requester@example.com"
ses_approver_test_email  = "approver@example.com"
```

Add these three keys to `terraform.tfvars.example` (with placeholder values) so they are documented for future developers.

### 6. Verify the setup manually

After `terraform apply` and clicking verification links:

```bash
# Send a test email from the CLI to confirm IAM + SES are wired correctly
aws ses send-email \
  --from "noreply@example.com" \
  --destination "ToAddresses=approver@example.com" \
  --message "Subject={Data=OTP test,Charset=UTF-8},Body={Text={Data=Hello,Charset=UTF-8}}" \
  --region us-east-1
```

A `MessageId` in the response confirms the IAM policy and SES identity are correct. A `MessageRejected` error usually means the recipient is not verified (sandbox restriction).

## Definition of Done

- [ ] `aws_ses_email_identity` resources for requester and approver test addresses added and verified (verification link clicked)
- [ ] `aws_sesv2_configuration_set` resource created and deployed
- [ ] `aws_iam_role_policy.lambda_ses` inline policy attached to `lambda_backend` role with `ses:SendEmail` and `ses:SendRawEmail`
- [ ] `AWS__SES__FromAddress`, `AWS__SES__ConfigurationSet`, and `AWS__SES__Enabled` environment variables added to `aws_lambda_function.backend`
- [ ] New input variables (`ses_from_address`, `ses_requester_test_email`, `ses_approver_test_email`) added to `variables.tf` and `terraform.tfvars.example`
- [ ] `terraform plan` shows no unexpected changes after initial apply
- [ ] Manual AWS CLI test email sent successfully (non-empty `MessageId` returned)
