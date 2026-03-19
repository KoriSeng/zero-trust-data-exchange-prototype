output "service_url" {
  description = "Backend ECS service base URL"
  value       = "http://${aws_lb.backend.dns_name}"
}

output "api_gateway_url" {
  description = "HTTPS API Gateway URL fronting ECS backend"
  value       = aws_apigatewayv2_stage.default.invoke_url
}

output "api_gateway_id" {
  description = "HTTP API Gateway ID fronting ECS backend"
  value       = aws_apigatewayv2_api.backend.id
}

output "alb_dns_name" {
  description = "ALB DNS name for backend ECS service"
  value       = aws_lb.backend.dns_name
}

output "cluster_name" {
  description = "ECS cluster name"
  value       = aws_ecs_cluster.backend.name
}

output "ecr_repository_url" {
  description = "ECR repository URL for backend image"
  value       = aws_ecr_repository.backend.repository_url
}

output "docdb_endpoint" {
  description = "DocumentDB cluster endpoint for ECS backend"
  value       = aws_docdb_cluster.main.endpoint
}
