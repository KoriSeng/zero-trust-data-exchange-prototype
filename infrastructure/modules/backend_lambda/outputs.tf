output "api_gateway_url" {
  description = "API Gateway HTTP API invoke URL"
  value       = aws_apigatewayv2_stage.default.invoke_url
}

output "api_gateway_id" {
  description = "API Gateway HTTP API ID"
  value       = aws_apigatewayv2_api.backend.id
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

output "lambda_error_alarm_name" {
  description = "CloudWatch alarm name for backend Lambda error spikes"
  value       = aws_cloudwatch_metric_alarm.lambda_error_spike.alarm_name
}

output "monitoring_dashboard_name" {
  description = "CloudWatch dashboard name for backend monitoring overview"
  value       = aws_cloudwatch_dashboard.prototype_overview.dashboard_name
}

output "docdb_security_group_id" {
  description = "DocumentDB security group ID"
  value       = aws_security_group.docdb.id
}
