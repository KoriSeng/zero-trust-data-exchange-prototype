output "bucket_name" {
  description = "POC bucket name"
  value       = aws_s3_bucket.poc.bucket
}

output "bucket_arn" {
  description = "POC bucket ARN"
  value       = aws_s3_bucket.poc.arn
}

output "presigner_role_arn" {
  description = "IAM role ARN for generating pre-signed URLs"
  value       = var.create_presigner_role && var.allow_account_id != null ? aws_iam_role.poc_presigner[0].arn : null
}
