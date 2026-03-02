---
type: Task
task_id: TASK-017
title: "Configure CloudWatch Logs Infrastructure"
owner: STK-001
status: "Completed"
related_milestone: MS-006
---

## Description

Set up CloudWatch Logs infrastructure using Terraform, including log groups, streams, retention policies, and IAM permissions for Lambda functions and other services to write audit logs.

## Definition of Done

- [x] CloudWatch Log Groups created for each component
- [x] Retention policies configured
- [x] IAM permissions configured for log writing
- [x] Log encryption enabled
- [ ] Infrastructure deployed and tested

## Implementation Notes

Log groups provisioned in `infrastructure/modules/backend_lambda/main.tf`:
- `/aws/lambda/{prefix}-backend` — Lambda function runtime logs (90-day retention)
- `/zero-trust/{prefix}/audit` — Structured audit events log group (90-day retention)
- `/aws/apigateway/{prefix}-backend` — API Gateway access logs (90-day retention)

KMS encryption: `aws_kms_key.cloudwatch_logs` with key rotation enabled; policy grants CloudWatch Logs service principal encrypt/decrypt access scoped by log ARN context.

IAM: `aws_iam_role_policy.lambda_cloudwatch_logs` grants scoped `CreateLogStream`, `PutLogEvents`, `DescribeLogStreams` on the Lambda and audit log groups + KMS decrypt/GenerateDataKey.

Lambda env var `CloudWatch__AuditLogGroup` passes the audit log group name to the application.

"Infrastructure deployed and tested" requires a `terraform apply` against AWS — cannot be ticked without a live deployment.
