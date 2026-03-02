terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}

locals {
  name_prefix  = "${var.project_name}-${var.environment}"
  zip_path     = "${path.module}/../../../build/backend-lambda.zip"
  build_script = "${path.module}/../../../implementation/backend/scripts/build-lambda.sh"

  # DocumentDB connection string — .NET MongoDB Driver format with TLS
  # CA bundle is bundled in the Lambda zip at /var/task/rds-combined-ca-bundle.pem
  docdb_connection_string = "mongodb://${var.docdb_master_username}:${var.docdb_master_password}@${aws_docdb_cluster.main.endpoint}:27017/?tls=true&tlsCAFile=/var/task/rds-combined-ca-bundle.pem&replicaSet=rs0&readPreference=secondaryPreferred&retryWrites=false"
}

# ============================================================================
# VPC — Lambda in public subnets (internet via IGW); DocumentDB in private subnets
# Lambda accesses: DocumentDB (within VPC), S3 (free Gateway endpoint),
# CloudWatch Logs + other AWS APIs (outbound directly through IGW — no NAT Gateway)
# No NAT Gateway — avoids per-AZ hourly cost; acceptable for academic prototype
# No VPC Interface Endpoints — avoids per-AZ hourly endpoint costs
# ============================================================================

data "aws_availability_zones" "available" {
  state = "available"
}

resource "aws_vpc" "main" {
  cidr_block           = "10.0.0.0/16"
  enable_dns_hostnames = true
  enable_dns_support   = true

  tags = { Name = "${local.name_prefix}-vpc" }
}

# Public subnets — Lambda runs here; ENIs get public IPs, outbound via IGW
resource "aws_subnet" "public_a" {
  vpc_id                  = aws_vpc.main.id
  cidr_block              = "10.0.0.0/24"
  availability_zone       = data.aws_availability_zones.available.names[0]
  map_public_ip_on_launch = true

  tags = { Name = "${local.name_prefix}-public-a" }
}

resource "aws_subnet" "public_b" {
  vpc_id                  = aws_vpc.main.id
  cidr_block              = "10.0.3.0/24"
  availability_zone       = data.aws_availability_zones.available.names[1]
  map_public_ip_on_launch = true

  tags = { Name = "${local.name_prefix}-public-b" }
}

resource "aws_subnet" "private_a" {
  vpc_id            = aws_vpc.main.id
  cidr_block        = "10.0.1.0/24"
  availability_zone = data.aws_availability_zones.available.names[0]

  tags = { Name = "${local.name_prefix}-private-a" }
}

resource "aws_subnet" "private_b" {
  vpc_id            = aws_vpc.main.id
  cidr_block        = "10.0.2.0/24"
  availability_zone = data.aws_availability_zones.available.names[1]

  tags = { Name = "${local.name_prefix}-private-b" }
}

# Internet Gateway — attached to VPC; Lambda in public subnets uses this for outbound traffic
resource "aws_internet_gateway" "main" {
  vpc_id = aws_vpc.main.id
  tags   = { Name = "${local.name_prefix}-igw" }
}

# Public route table — routes internet traffic out via IGW
resource "aws_route_table" "public" {
  vpc_id = aws_vpc.main.id
  tags   = { Name = "${local.name_prefix}-public-rt" }

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.main.id
  }
}

resource "aws_route_table_association" "public_a" {
  subnet_id      = aws_subnet.public_a.id
  route_table_id = aws_route_table.public.id
}

resource "aws_route_table_association" "public_b" {
  subnet_id      = aws_subnet.public_b.id
  route_table_id = aws_route_table.public.id
}

# Private route table — DocumentDB subnets; no internet route needed
resource "aws_route_table" "private" {
  vpc_id = aws_vpc.main.id
  tags   = { Name = "${local.name_prefix}-private-rt" }
}

resource "aws_route_table_association" "private_a" {
  subnet_id      = aws_subnet.private_a.id
  route_table_id = aws_route_table.private.id
}

resource "aws_route_table_association" "private_b" {
  subnet_id      = aws_subnet.private_b.id
  route_table_id = aws_route_table.private.id
}

# ============================================================================
# VPC Endpoints
# ============================================================================

# S3 Gateway Endpoint — free, routes S3 traffic within AWS backbone (attached to Lambda's public route table)
resource "aws_vpc_endpoint" "s3" {
  vpc_id            = aws_vpc.main.id
  service_name      = "com.amazonaws.${var.aws_region}.s3"
  vpc_endpoint_type = "Gateway"
  route_table_ids   = [aws_route_table.public.id]

  tags = { Name = "${local.name_prefix}-s3-endpoint" }
}

# ============================================================================
# Security Groups
# ============================================================================

resource "aws_security_group" "lambda" {
  name        = "${local.name_prefix}-lambda-sg"
  description = "Security group for backend Lambda function"
  vpc_id      = aws_vpc.main.id

  # Outbound HTTPS — S3 (via gateway endpoint) and internet (direct via IGW, no NAT)
  egress {
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
    description = "HTTPS outbound (S3 gateway endpoint + internet via IGW)"
  }

  tags = { Name = "${local.name_prefix}-lambda-sg" }
}

resource "aws_security_group" "docdb" {
  name        = "${local.name_prefix}-docdb-sg"
  description = "Security group for DocumentDB cluster"
  vpc_id      = aws_vpc.main.id

  tags = { Name = "${local.name_prefix}-docdb-sg" }
}

# Separate rules to break the circular dependency between lambda and docdb security groups
resource "aws_security_group_rule" "lambda_to_docdb" {
  type                     = "egress"
  from_port                = 27017
  to_port                  = 27017
  protocol                 = "tcp"
  security_group_id        = aws_security_group.lambda.id
  source_security_group_id = aws_security_group.docdb.id
  description              = "DocumentDB"
}

resource "aws_security_group_rule" "docdb_from_lambda" {
  type                     = "ingress"
  from_port                = 27017
  to_port                  = 27017
  protocol                 = "tcp"
  security_group_id        = aws_security_group.docdb.id
  source_security_group_id = aws_security_group.lambda.id
  description              = "Allow Lambda to connect to DocumentDB"
}

# ============================================================================
# DocumentDB
# ============================================================================

resource "aws_docdb_subnet_group" "main" {
  name       = "${local.name_prefix}-docdb-subnets"
  subnet_ids = [aws_subnet.private_a.id, aws_subnet.private_b.id]

  tags = { Name = "${local.name_prefix}-docdb-subnet-group" }
}

resource "aws_docdb_cluster_parameter_group" "main" {
  family      = "docdb5.0"
  name        = "${local.name_prefix}-docdb-params"
  description = "DocumentDB cluster parameter group"

  parameter {
    name  = "tls"
    value = "enabled"
  }
}

resource "aws_docdb_cluster" "main" {
  cluster_identifier              = "${local.name_prefix}-docdb"
  engine                          = "docdb"
  engine_version                  = "5.0.0"
  master_username                 = var.docdb_master_username
  master_password                 = var.docdb_master_password
  db_subnet_group_name            = aws_docdb_subnet_group.main.name
  db_cluster_parameter_group_name = aws_docdb_cluster_parameter_group.main.name
  vpc_security_group_ids          = [aws_security_group.docdb.id]
  skip_final_snapshot             = true # prototype — no final snapshot on destroy
  deletion_protection             = false
  storage_encrypted               = true

  tags = { Name = "${local.name_prefix}-docdb" }
}

resource "aws_docdb_cluster_instance" "main" {
  count              = 1
  identifier         = "${local.name_prefix}-docdb-0"
  cluster_identifier = aws_docdb_cluster.main.id
  instance_class     = var.docdb_instance_class

  tags = { Name = "${local.name_prefix}-docdb-instance-0" }
}

# ============================================================================
# Lambda Build (null_resource triggers dotnet publish on code changes)
# ============================================================================

# Hash all .cs source files + csproj to detect when a rebuild is needed
data "external" "backend_source_hash" {
  program = ["bash", "-c", <<-EOT
    find ${path.module}/../../../implementation/backend -name "*.cs" -o -name "*.csproj" \
      | sort | xargs sha256sum 2>/dev/null | sha256sum | awk '{print "{\"hash\":\""$1"\"}"}'
  EOT
  ]
}

resource "null_resource" "build_backend_lambda" {
  triggers = {
    source_hash = data.external.backend_source_hash.result["hash"]
  }

  provisioner "local-exec" {
    command = "bash ${local.build_script}"
  }
}

# ============================================================================
# Lambda IAM Role + Policies
# ============================================================================

resource "aws_iam_role" "lambda_backend" {
  name = "${local.name_prefix}-backend-lambda-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "lambda.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })
}

# VPC access (create/delete ENIs) + basic CloudWatch Logs via AWSLambdaVPCAccessExecutionRole
resource "aws_iam_role_policy_attachment" "lambda_vpc_execution" {
  role       = aws_iam_role.lambda_backend.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaVPCAccessExecutionRole"
}

resource "aws_iam_role_policy" "lambda_s3" {
  name = "${local.name_prefix}-lambda-s3-policy"
  role = aws_iam_role.lambda_backend.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = ["s3:GetObject", "s3:HeadObject", "s3:ListBucket"]
        Resource = [
          "arn:aws:s3:::${var.s3_data_bucket}",
          "arn:aws:s3:::${var.s3_data_bucket}/*"
        ]
      },
      {
        Effect = "Allow"
        Action = ["s3:PutObject", "s3:GetObject", "s3:ListBucket"]
        Resource = [
          "arn:aws:s3:::${var.s3_requests_bucket}",
          "arn:aws:s3:::${var.s3_requests_bucket}/*"
        ]
      }
    ]
  })
}

resource "aws_iam_role_policy" "lambda_stepfunctions" {
  name = "${local.name_prefix}-lambda-sfn-policy"
  role = aws_iam_role.lambda_backend.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect   = "Allow"
      Action   = ["states:StartExecution", "states:DescribeExecution"]
      Resource = var.step_functions_approval_arn != "" ? [var.step_functions_approval_arn] : ["arn:aws:states:*:*:stateMachine:*"]
    }]
  })
}

# Scoped CloudWatch Logs permissions for Lambda (TASK-017)
# Grants explicit write access to the specific log groups; supplements AWSLambdaVPCAccessExecutionRole
resource "aws_iam_role_policy" "lambda_cloudwatch_logs" {
  name = "${local.name_prefix}-lambda-cw-logs-policy"
  role = aws_iam_role.lambda_backend.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "logs:CreateLogStream",
          "logs:PutLogEvents",
          "logs:DescribeLogStreams"
        ]
        Resource = [
          "${aws_cloudwatch_log_group.lambda_backend.arn}:*",
          "${aws_cloudwatch_log_group.audit.arn}:*"
        ]
      },
      {
        Effect   = "Allow"
        Action   = ["kms:Decrypt", "kms:GenerateDataKey"]
        Resource = aws_kms_key.cloudwatch_logs.arn
      }
    ]
  })
}

# ============================================================================
# KMS Key for CloudWatch Logs Encryption (TASK-017)
# ============================================================================

data "aws_caller_identity" "current" {}

resource "aws_kms_key" "cloudwatch_logs" {
  description             = "KMS key for CloudWatch Logs encryption - ${local.name_prefix}"
  deletion_window_in_days = 7
  enable_key_rotation     = true

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "Enable IAM User Permissions"
        Effect = "Allow"
        Principal = {
          AWS = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:root"
        }
        Action   = "kms:*"
        Resource = "*"
      },
      {
        Sid    = "Allow CloudWatch Logs Service"
        Effect = "Allow"
        Principal = {
          Service = "logs.${var.aws_region}.amazonaws.com"
        }
        Action = [
          "kms:Encrypt",
          "kms:Decrypt",
          "kms:ReEncrypt*",
          "kms:GenerateDataKey",
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

  tags = { Name = "${local.name_prefix}-cloudwatch-logs-key" }
}

resource "aws_kms_alias" "cloudwatch_logs" {
  name          = "alias/${local.name_prefix}-cloudwatch-logs"
  target_key_id = aws_kms_key.cloudwatch_logs.key_id
}

# ============================================================================
# CloudWatch Log Groups (TASK-017)
# All groups: 90-day retention, KMS encrypted
# ============================================================================

resource "aws_cloudwatch_log_group" "lambda_backend" {
  name              = "/aws/lambda/${local.name_prefix}-backend"
  retention_in_days = 90
  kms_key_id        = aws_kms_key.cloudwatch_logs.arn
}

# Dedicated audit event log group — backend writes structured audit events here
resource "aws_cloudwatch_log_group" "audit" {
  name              = "/zero-trust/${local.name_prefix}/audit"
  retention_in_days = 90
  kms_key_id        = aws_kms_key.cloudwatch_logs.arn
}

# ============================================================================
# Lambda Function
# ============================================================================

resource "aws_lambda_function" "backend" {
  function_name = "${local.name_prefix}-backend"
  role          = aws_iam_role.lambda_backend.arn
  runtime       = "dotnet10"          # .NET 10 managed runtime
  handler       = "ZeroTrust.Backend" # assembly name for Amazon.Lambda.AspNetCoreServer.Hosting
  filename      = local.zip_path
  timeout       = var.lambda_timeout_s
  memory_size   = var.lambda_memory_mb

  # Recompute source_code_hash only when the zip changes
  source_code_hash = fileexists(local.zip_path) ? filebase64sha256(local.zip_path) : null

  vpc_config {
    subnet_ids         = [aws_subnet.public_a.id, aws_subnet.public_b.id]
    security_group_ids = [aws_security_group.lambda.id]
  }

  environment {
    variables = {
      # ASP.NET Core configuration (double-underscore = nested key separator)
      ConnectionStrings__MongoDB              = local.docdb_connection_string
      MongoDb__DatabaseName                   = "zero_trust_db"
      AWS__Region                             = var.aws_region
      AWS__S3__DataBucket                     = var.s3_data_bucket
      AWS__S3__RequestsBucket                 = var.s3_requests_bucket
      AWS__StepFunctions__ApprovalWorkflowArn = var.step_functions_approval_arn
      AWS__StepFunctions__Enabled             = var.step_functions_approval_arn != "" ? "true" : "false"
      # Prevent the DatabaseSeederHostedService from running on Lambda cold starts
      SeedDatabase = "false"
      # Suppress LocalStack override — use real AWS endpoints in Lambda
      AWS__ServiceUrl        = ""
      ASPNETCORE_ENVIRONMENT = "Production"
      # CloudWatch Logs audit log group name (TASK-017)
      CloudWatch__AuditLogGroup = aws_cloudwatch_log_group.audit.name
    }
  }

  depends_on = [
    null_resource.build_backend_lambda,
    aws_docdb_cluster_instance.main,
    aws_iam_role_policy_attachment.lambda_vpc_execution,
    aws_cloudwatch_log_group.lambda_backend,
    aws_iam_role_policy.lambda_cloudwatch_logs,
  ]

  tags = { Name = "${local.name_prefix}-backend" }
}

# ============================================================================
# API Gateway HTTP API
# ============================================================================

resource "aws_apigatewayv2_api" "backend" {
  name          = "${local.name_prefix}-backend-api"
  protocol_type = "HTTP"
  description   = "Zero Trust Data Exchange backend API"
}

# JWT Authorizer backed by Cognito User Pool
resource "aws_apigatewayv2_authorizer" "cognito" {
  api_id           = aws_apigatewayv2_api.backend.id
  authorizer_type  = "JWT"
  identity_sources = ["$request.header.Authorization"]
  name             = "cognito-jwt"

  jwt_configuration {
    issuer   = "https://cognito-idp.${var.aws_region}.amazonaws.com/${var.cognito_user_pool_id}"
    audience = [var.cognito_app_client_id]
  }
}

# Lambda integration (proxy — passes all request context to Lambda)
resource "aws_apigatewayv2_integration" "backend" {
  api_id                 = aws_apigatewayv2_api.backend.id
  integration_type       = "AWS_PROXY"
  integration_uri        = aws_lambda_function.backend.invoke_arn
  payload_format_version = "2.0" # matches LambdaEventSource.HttpApi
}

# /health — public, no authorizer
resource "aws_apigatewayv2_route" "health" {
  api_id    = aws_apigatewayv2_api.backend.id
  route_key = "GET /health"
  target    = "integrations/${aws_apigatewayv2_integration.backend.id}"
}

# All other routes — require valid Cognito JWT
resource "aws_apigatewayv2_route" "default" {
  api_id             = aws_apigatewayv2_api.backend.id
  route_key          = "$default"
  target             = "integrations/${aws_apigatewayv2_integration.backend.id}"
  authorization_type = "JWT"
  authorizer_id      = aws_apigatewayv2_authorizer.cognito.id
}

# Default stage with auto-deploy
resource "aws_apigatewayv2_stage" "default" {
  api_id      = aws_apigatewayv2_api.backend.id
  name        = "$default"
  auto_deploy = true

  access_log_settings {
    destination_arn = aws_cloudwatch_log_group.apigw.arn
    format = jsonencode({
      requestId               = "$context.requestId"
      sourceIp                = "$context.identity.sourceIp"
      requestTime             = "$context.requestTime"
      httpMethod              = "$context.httpMethod"
      routeKey                = "$context.routeKey"
      status                  = "$context.status"
      protocol                = "$context.protocol"
      responseLength          = "$context.responseLength"
      integrationErrorMessage = "$context.integrationErrorMessage"
    })
  }
}

resource "aws_cloudwatch_log_group" "apigw" {
  name              = "/aws/apigateway/${local.name_prefix}-backend"
  retention_in_days = 90
  kms_key_id        = aws_kms_key.cloudwatch_logs.arn
}

# Allow API Gateway to invoke the Lambda function
resource "aws_lambda_permission" "apigw" {
  statement_id  = "AllowAPIGatewayInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.backend.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.backend.execution_arn}/*/*"
}
