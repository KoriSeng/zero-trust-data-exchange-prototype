output "api_gateway_url" {
  description = "API Gateway HTTP API invoke URL"
  value       = aws_apigatewayv2_stage.default.invoke_url
}

output "lambda_function_name" {
  description = "Backend Lambda function name"
  value       = aws_lambda_function.backend.function_name
}

output "lambda_function_arn" {
  description = "Backend Lambda function ARN"
  value       = aws_lambda_function.backend.arn
}

output "docdb_endpoint" {
  description = "DocumentDB cluster endpoint"
  value       = aws_docdb_cluster.main.endpoint
}

output "docdb_reader_endpoint" {
  description = "DocumentDB cluster reader endpoint"
  value       = aws_docdb_cluster.main.reader_endpoint
}

output "vpc_id" {
  description = "VPC ID"
  value       = aws_vpc.main.id
}

output "private_subnet_ids" {
  description = "Private subnet IDs (DocumentDB only — no internet route)"
  value       = [aws_subnet.private_a.id, aws_subnet.private_b.id]
}

output "public_subnet_ids" {
  description = "Public subnet IDs (Lambda — internet via IGW)"
  value       = [aws_subnet.public_a.id, aws_subnet.public_b.id]
}

# ── CloudWatch Logging Outputs (TASK-017) ─────────────────────────────────────

output "cloudwatch_kms_key_arn" {
  description = "KMS key ARN used for CloudWatch log group encryption"
  value       = aws_kms_key.cloudwatch_logs.arn
}

output "lambda_log_group_name" {
  description = "CloudWatch Log Group name for Lambda function logs"
  value       = aws_cloudwatch_log_group.lambda_backend.name
}

output "audit_log_group_name" {
  description = "CloudWatch Log Group name for structured audit events"
  value       = aws_cloudwatch_log_group.audit.name
}

output "apigw_log_group_name" {
  description = "CloudWatch Log Group name for API Gateway access logs"
  value       = aws_cloudwatch_log_group.apigw.name
}
