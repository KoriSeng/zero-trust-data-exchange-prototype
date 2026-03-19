# Archive IDP code
data "archive_file" "idp_code" {
  type        = "zip"
  source_dir  = var.source_directory
  output_path = "${path.module}/.terraform/idp-${var.idp_name}.zip"

  excludes = [
    "test",
    "package-lock.json",
    ".gitignore"
  ]
}

# Explicit log group — prevents Lambda auto-creating one with no retention policy
resource "aws_cloudwatch_log_group" "idp" {
  name              = "/aws/lambda/${var.project_name}-idp-${var.idp_name}"
  retention_in_days = 30
  kms_key_id        = var.logs_kms_key_arn != "" ? var.logs_kms_key_arn : null

  tags = var.tags
}

# Lambda Function
resource "aws_lambda_function" "idp" {
  filename         = data.archive_file.idp_code.output_path
  function_name    = "${var.project_name}-idp-${var.idp_name}"
  role             = var.lambda_exec_role_arn
  handler          = "handler.handler"
  runtime          = var.lambda_runtime
  timeout          = var.lambda_timeout
  memory_size      = var.lambda_memory_size
  source_code_hash = data.archive_file.idp_code.output_base64sha256

  environment {
    variables = {
      ISSUER_NAME = var.idp_display_name
      USER_SET    = var.idp_user_set
      PORT        = "8080"
    }
  }

  tags = var.tags

  depends_on = [aws_cloudwatch_log_group.idp]
}

# Function URL for IDP
resource "aws_lambda_function_url" "idp" {
  function_name      = aws_lambda_function.idp.function_name
  authorization_type = var.authorization_type
  cors {
    allow_origins = var.cors_origins
    allow_methods = var.cors_methods
    allow_headers = var.cors_headers
  }
}

# Resource-based policy for public Lambda Function URL access
# Required when authorization_type = "NONE" to allow unauthenticated access
resource "aws_lambda_permission" "allow_public_access" {
  count                  = var.authorization_type == "NONE" ? 1 : 0
  statement_id           = "AllowPublicAccess"
  action                 = "lambda:InvokeFunctionUrl"
  function_name          = aws_lambda_function.idp.function_name
  principal              = "*"
  function_url_auth_type = "NONE"
}

# Additional permission for regular lambda invocation (Function URL needs both)
resource "aws_lambda_permission" "allow_public_invoke" {
  count         = var.authorization_type == "NONE" ? 1 : 0
  statement_id  = "AllowPublicInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.idp.function_name
  principal     = "*"
}
