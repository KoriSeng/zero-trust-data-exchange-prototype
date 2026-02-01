output "lambda_function_arn" {
  description = "ARN of the Lambda function"
  value       = aws_lambda_function.idp.arn
}

output "lambda_function_name" {
  description = "Name of the Lambda function"
  value       = aws_lambda_function.idp.function_name
}

output "function_url" {
  description = "Function URL for the IDP"
  value       = aws_lambda_function_url.idp.function_url
}

output "public_access_enabled" {
  description = "Whether public access is enabled for the Function URL"
  value       = var.authorization_type == "NONE"
}
