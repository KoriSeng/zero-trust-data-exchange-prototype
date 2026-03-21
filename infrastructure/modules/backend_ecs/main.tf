locals {
  name_prefix     = "${var.project_name}-${var.environment}"
  repository_name = "${local.name_prefix}-backend"
  image_tag       = "latest"
  image_uri       = "${aws_ecr_repository.backend.repository_url}:${local.image_tag}"
  backend_dir     = "${path.module}/../../../implementation/backend"
  # Prototype fallback: tlsInsecure=true avoids container CA-chain issues with DocDB.
  docdb_connection_string = "mongodb://${var.docdb_master_username}:${var.docdb_master_password}@${aws_docdb_cluster.main.endpoint}:27017/?tls=true&tlsInsecure=true&replicaSet=rs0&readPreference=secondaryPreferred&retryWrites=false"
}

# ============================================================================
# Networking (VPC + Public/Private Subnets)
# ============================================================================

resource "aws_vpc" "main" {
  cidr_block           = "10.10.0.0/16"
  enable_dns_hostnames = true
  enable_dns_support   = true
  tags                 = { Name = "${local.name_prefix}-ecs-vpc" }
}

resource "aws_subnet" "public_a" {
  vpc_id                  = aws_vpc.main.id
  cidr_block              = "10.10.0.0/24"
  availability_zone       = var.subnet_az_a
  map_public_ip_on_launch = true
  tags                    = { Name = "${local.name_prefix}-ecs-public-a" }
}

resource "aws_subnet" "public_b" {
  vpc_id                  = aws_vpc.main.id
  cidr_block              = "10.10.1.0/24"
  availability_zone       = var.subnet_az_b
  map_public_ip_on_launch = true
  tags                    = { Name = "${local.name_prefix}-ecs-public-b" }
}

resource "aws_subnet" "private_a" {
  vpc_id            = aws_vpc.main.id
  cidr_block        = "10.10.10.0/24"
  availability_zone = var.subnet_az_a
  tags              = { Name = "${local.name_prefix}-ecs-private-a" }
}

resource "aws_subnet" "private_b" {
  vpc_id            = aws_vpc.main.id
  cidr_block        = "10.10.11.0/24"
  availability_zone = var.subnet_az_b
  tags              = { Name = "${local.name_prefix}-ecs-private-b" }
}

resource "aws_internet_gateway" "main" {
  vpc_id = aws_vpc.main.id
  tags   = { Name = "${local.name_prefix}-ecs-igw" }
}

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.main.id
  tags   = { Name = "${local.name_prefix}-ecs-public-rt" }

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

resource "aws_route_table" "private" {
  vpc_id = aws_vpc.main.id
  tags   = { Name = "${local.name_prefix}-ecs-private-rt" }
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
# DocumentDB
# ============================================================================

resource "aws_security_group" "docdb" {
  name        = "${local.name_prefix}-ecs-docdb-sg"
  description = "DocumentDB security group for ECS backend"
  vpc_id      = aws_vpc.main.id
}

resource "aws_docdb_subnet_group" "main" {
  name       = "${local.name_prefix}-ecs-docdb-subnets"
  subnet_ids = [aws_subnet.private_a.id, aws_subnet.private_b.id]
  tags       = { Name = "${local.name_prefix}-ecs-docdb-subnet-group" }
}

resource "aws_docdb_cluster_parameter_group" "main" {
  family      = "docdb5.0"
  name        = "${local.name_prefix}-ecs-docdb-params"
  description = "DocumentDB parameter group for ECS backend"

  parameter {
    name  = "tls"
    value = "enabled"
  }
}

resource "aws_docdb_cluster" "main" {
  cluster_identifier              = "${local.name_prefix}-ecs-docdb"
  engine                          = "docdb"
  engine_version                  = "5.0.0"
  master_username                 = var.docdb_master_username
  master_password                 = var.docdb_master_password
  db_subnet_group_name            = aws_docdb_subnet_group.main.name
  db_cluster_parameter_group_name = aws_docdb_cluster_parameter_group.main.name
  vpc_security_group_ids          = [aws_security_group.docdb.id]
  skip_final_snapshot             = true
  deletion_protection             = false
  storage_encrypted               = true
  tags                            = { Name = "${local.name_prefix}-ecs-docdb" }
}

resource "aws_docdb_cluster_instance" "main" {
  count              = 1
  identifier         = "${local.name_prefix}-ecs-docdb-0"
  cluster_identifier = aws_docdb_cluster.main.id
  instance_class     = var.docdb_instance_class
  tags               = { Name = "${local.name_prefix}-ecs-docdb-instance-0" }
}

# ============================================================================
# ECR + Image Build/Push
# ============================================================================

resource "aws_ecr_repository" "backend" {
  name                 = local.repository_name
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }
}

resource "null_resource" "build_and_push_backend_image" {
  triggers = {
    always_run = timestamp()
  }

  provisioner "local-exec" {
    command     = <<-EOT
      set -euo pipefail
      aws ecr get-login-password --region ${var.aws_region} \
        | docker login --username AWS --password-stdin ${aws_ecr_repository.backend.repository_url}
      docker build -t ${local.repository_name}:${local.image_tag} ${local.backend_dir}
      docker tag ${local.repository_name}:${local.image_tag} ${local.image_uri}
      docker push ${local.image_uri}
    EOT
    interpreter = ["bash", "-c"]
  }
}

# ============================================================================
# ECS + ALB
# ============================================================================

resource "aws_cloudwatch_log_group" "ecs_backend" {
  name              = "/ecs/${local.name_prefix}-backend"
  retention_in_days = 30
}

resource "aws_ecs_cluster" "backend" {
  name = "${local.name_prefix}-backend"
}

resource "aws_iam_role" "task_execution" {
  name = "${local.name_prefix}-ecs-task-execution"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Principal = {
        Service = "ecs-tasks.amazonaws.com"
      }
      Action = "sts:AssumeRole"
    }]
  })
}

resource "aws_iam_role_policy_attachment" "task_execution_default" {
  role       = aws_iam_role.task_execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

resource "aws_iam_role" "task_role" {
  name = "${local.name_prefix}-ecs-task"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Principal = {
        Service = "ecs-tasks.amazonaws.com"
      }
      Action = "sts:AssumeRole"
    }]
  })
}

resource "aws_iam_role_policy" "task_app" {
  name = "${local.name_prefix}-ecs-task-app"
  role = aws_iam_role.task_role.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = concat(
      [{
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
      }],
      var.step_functions_approval_arn != "" ? [{
        Effect   = "Allow"
        Action   = ["states:StartExecution", "states:DescribeExecution"]
        Resource = [var.step_functions_approval_arn]
        },
        {
          Effect   = "Allow"
          Action   = ["states:SendTaskSuccess", "states:SendTaskFailure"]
          Resource = ["*"]
        },
        {
          Effect = "Allow"
          Action = [
            "sqs:ReceiveMessage",
            "sqs:DeleteMessage",
            "sqs:ChangeMessageVisibility",
            "sqs:GetQueueAttributes",
            "sqs:GetQueueUrl"
          ]
          Resource = compact([
            var.step_functions_approval_decision_queue_arn,
            var.step_functions_approval_otp_dispatch_queue_arn,
            var.step_functions_claim_callback_queue_arn,
            var.step_functions_claim_timeout_queue_arn
          ])
      }] : []
    )
  })
}

resource "aws_security_group" "alb" {
  name        = "${local.name_prefix}-backend-alb-sg"
  description = "ALB ingress for backend ECS"
  vpc_id      = aws_vpc.main.id

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = [aws_vpc.main.cidr_block]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_security_group" "service" {
  name        = "${local.name_prefix}-backend-ecs-sg"
  description = "ECS service security group"
  vpc_id      = aws_vpc.main.id

  ingress {
    from_port       = var.container_port
    to_port         = var.container_port
    protocol        = "tcp"
    security_groups = [aws_security_group.alb.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_security_group_rule" "docdb_from_ecs" {
  type                     = "ingress"
  from_port                = 27017
  to_port                  = 27017
  protocol                 = "tcp"
  security_group_id        = aws_security_group.docdb.id
  source_security_group_id = aws_security_group.service.id
  description              = "Allow ECS service to connect to DocumentDB"
}

resource "aws_lb" "backend" {
  name               = "${replace(local.name_prefix, "_", "-")}-ecs"
  internal           = true
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = [aws_subnet.private_a.id, aws_subnet.private_b.id]
}

resource "aws_lb_target_group" "backend" {
  name        = "${substr(replace(local.name_prefix, "_", "-"), 0, 15)}-ecs-backend"
  port        = var.container_port
  protocol    = "HTTP"
  target_type = "ip"
  vpc_id      = aws_vpc.main.id

  health_check {
    enabled             = true
    path                = "/health"
    port                = tostring(var.container_port)
    matcher             = "200"
    interval            = 30
    healthy_threshold   = 2
    unhealthy_threshold = 3
  }
}

resource "aws_lb_listener" "backend_http" {
  load_balancer_arn = aws_lb.backend.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.backend.arn
  }
}

resource "aws_ecs_task_definition" "backend" {
  family                   = "${local.name_prefix}-backend"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = tostring(var.cpu)
  memory                   = tostring(var.memory)
  execution_role_arn       = aws_iam_role.task_execution.arn
  task_role_arn            = aws_iam_role.task_role.arn

  container_definitions = jsonencode([{
    name      = "backend"
    image     = local.image_uri
    essential = true
    portMappings = [{
      containerPort = var.container_port
      protocol      = "tcp"
    }]
    environment = [
      { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
      { name = "ASPNETCORE_URLS", value = "http://+:${var.container_port}" },
      { name = "Kestrel__Endpoints__Http__Url", value = "http://0.0.0.0:${var.container_port}" },
      { name = "ConnectionStrings__MongoDB", value = local.docdb_connection_string },
      { name = "MongoDb__DatabaseName", value = "zero_trust_db" },
      { name = "AWS__Region", value = var.aws_region },
      { name = "AWS__ServiceUrl", value = "" },
      { name = "AWS__S3__DataBucket", value = var.s3_data_bucket },
      { name = "AWS__S3__RequestsBucket", value = var.s3_requests_bucket },
      { name = "AWS__StepFunctions__ApprovalWorkflowArn", value = var.step_functions_approval_arn },
      { name = "AWS__StepFunctions__Enabled", value = var.step_functions_approval_arn != "" ? "true" : "false" },
      { name = "AWS__StepFunctions__ApprovalDecisionQueueUrl", value = var.step_functions_approval_decision_queue_url },
      { name = "AWS__StepFunctions__OtpDispatchQueueUrl", value = var.step_functions_approval_otp_dispatch_queue_url },
      { name = "AWS__StepFunctions__ClaimCallbackQueueUrl", value = var.step_functions_claim_callback_queue_url },
      { name = "AWS__StepFunctions__ClaimTimeoutQueueUrl", value = var.step_functions_claim_timeout_queue_url },
      { name = "DeploymentTrigger", value = timestamp() },
      { name = "Cors__AllowedOrigins", value = join(",", var.cors_allowed_origins) },
      { name = "SeedDatabase", value = var.seed_database ? "true" : "false" },
      { name = "SeedData__OrgA__CognitoGroupName", value = var.seed_org_a_cognito_group_name },
      { name = "SeedData__OrgB__CognitoGroupName", value = var.seed_org_b_cognito_group_name },
      { name = "SeedData__OrgA__IdpIssuer", value = var.seed_org_a_idp_issuer },
      { name = "SeedData__OrgB__IdpIssuer", value = var.seed_org_b_idp_issuer }
    ]
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        awslogs-group         = aws_cloudwatch_log_group.ecs_backend.name
        awslogs-region        = var.aws_region
        awslogs-stream-prefix = "ecs"
      }
    }
  }])

  depends_on = [
    null_resource.build_and_push_backend_image,
    aws_docdb_cluster_instance.main
  ]
}

resource "aws_ecs_service" "backend" {
  name            = "${local.name_prefix}-backend"
  cluster         = aws_ecs_cluster.backend.id
  task_definition = aws_ecs_task_definition.backend.arn
  desired_count   = var.desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = [aws_subnet.public_a.id, aws_subnet.public_b.id]
    security_groups  = [aws_security_group.service.id]
    assign_public_ip = true
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.backend.arn
    container_name   = "backend"
    container_port   = var.container_port
  }

  depends_on = [aws_lb_listener.backend_http]
}

# ============================================================================
# API Gateway HTTP API (passthrough to ECS/ALB, no JWT authorizer)
# ============================================================================

resource "aws_security_group" "apigw_vpc_link" {
  name        = "${local.name_prefix}-apigw-vpc-link-sg"
  description = "Security group for API Gateway VPC Link to internal ALB"
  vpc_id      = aws_vpc.main.id

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_apigatewayv2_vpc_link" "backend" {
  name               = "${local.name_prefix}-ecs-vpc-link"
  security_group_ids = [aws_security_group.apigw_vpc_link.id]
  subnet_ids         = [aws_subnet.private_a.id, aws_subnet.private_b.id]
}

resource "aws_cloudwatch_log_group" "apigw" {
  name              = "/aws/apigateway/${local.name_prefix}-ecs-backend"
  retention_in_days = 30
}

resource "aws_apigatewayv2_api" "backend" {
  name          = "${local.name_prefix}-ecs-api"
  protocol_type = "HTTP"

  cors_configuration {
    allow_origins  = var.cors_allowed_origins
    allow_methods  = ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"]
    allow_headers  = ["authorization", "content-type", "x-amz-date", "x-api-key", "x-amz-security-token"]
    expose_headers = ["www-authenticate"]
    max_age        = 300
  }
}

resource "aws_apigatewayv2_integration" "backend" {
  api_id                 = aws_apigatewayv2_api.backend.id
  integration_type       = "HTTP_PROXY"
  integration_method     = "ANY"
  integration_uri        = aws_lb_listener.backend_http.arn
  connection_type        = "VPC_LINK"
  connection_id          = aws_apigatewayv2_vpc_link.backend.id
  payload_format_version = "1.0"
}

resource "aws_apigatewayv2_route" "default" {
  api_id    = aws_apigatewayv2_api.backend.id
  route_key = "$default"
  target    = "integrations/${aws_apigatewayv2_integration.backend.id}"
}

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
