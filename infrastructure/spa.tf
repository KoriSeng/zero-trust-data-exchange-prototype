locals {
  spa_bucket_name = "${var.project_name}-spa-${var.environment}-${data.aws_caller_identity.current.account_id}"
  spa_source_dir  = abspath("${path.root}/../implementation/spa")
  spa_source_files = sort(concat(
    tolist(fileset(local.spa_source_dir, "src/**")),
    tolist(fileset(local.spa_source_dir, "public/**")),
    tolist(fileset(local.spa_source_dir, "index.html")),
    tolist(fileset(local.spa_source_dir, "package.json")),
    tolist(fileset(local.spa_source_dir, "package-lock.json")),
    tolist(fileset(local.spa_source_dir, "vite.config.js"))
  ))
  spa_source_hash = sha256(join("", [for rel in local.spa_source_files : filesha256("${local.spa_source_dir}/${rel}")]))
  spa_runtime_hash = sha256(join("|", [
    module.cognito.user_pool_id,
    module.cognito.app_client_id,
    module.cognito.user_pool_domain,
    local.backend_api_base_url,
    aws_cloudfront_distribution.spa.domain_name
  ]))
  spa_tags = {
    Name        = "${var.project_name}-spa-${var.environment}"
    Environment = var.environment
    Project     = var.project_name
    Component   = "spa-hosting"
  }
}

resource "aws_s3_bucket" "spa" {
  bucket        = local.spa_bucket_name
  force_destroy = true

  tags = local.spa_tags
}

resource "aws_s3_bucket_public_access_block" "spa" {
  bucket                  = aws_s3_bucket.spa.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_cloudfront_origin_access_control" "spa" {
  name                              = "${var.project_name}-${var.environment}-spa-oac"
  origin_access_control_origin_type = "s3"
  signing_behavior                  = "always"
  signing_protocol                  = "sigv4"
}

resource "aws_cloudfront_cache_policy" "spa_app_shell" {
  name        = "${var.project_name}-${var.environment}-spa-app-shell"
  comment     = "Short cache for index.html and SPA client-side routes."
  default_ttl = 60
  max_ttl     = 300
  min_ttl     = 0

  parameters_in_cache_key_and_forwarded_to_origin {
    cookies_config {
      cookie_behavior = "none"
    }

    headers_config {
      header_behavior = "none"
    }

    query_strings_config {
      query_string_behavior = "none"
    }

    enable_accept_encoding_brotli = true
    enable_accept_encoding_gzip   = true
  }
}

resource "aws_cloudfront_cache_policy" "spa_static_assets" {
  name        = "${var.project_name}-${var.environment}-spa-static-assets"
  comment     = "Long cache for fingerprinted SPA assets."
  default_ttl = 86400
  max_ttl     = 31536000
  min_ttl     = 3600

  parameters_in_cache_key_and_forwarded_to_origin {
    cookies_config {
      cookie_behavior = "none"
    }

    headers_config {
      header_behavior = "none"
    }

    query_strings_config {
      query_string_behavior = "none"
    }

    enable_accept_encoding_brotli = true
    enable_accept_encoding_gzip   = true
  }
}

resource "aws_cloudfront_distribution" "spa" {
  enabled             = true
  wait_for_deployment = false
  default_root_object = "index.html"
  price_class         = "PriceClass_100"

  origin {
    domain_name              = aws_s3_bucket.spa.bucket_regional_domain_name
    origin_id                = "spa-s3-origin"
    origin_access_control_id = aws_cloudfront_origin_access_control.spa.id
  }

  default_cache_behavior {
    target_origin_id       = "spa-s3-origin"
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD", "OPTIONS"]
    cached_methods         = ["GET", "HEAD"]
    compress               = true
    cache_policy_id        = aws_cloudfront_cache_policy.spa_app_shell.id
  }

  ordered_cache_behavior {
    path_pattern           = "assets/*"
    target_origin_id       = "spa-s3-origin"
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD", "OPTIONS"]
    cached_methods         = ["GET", "HEAD"]
    compress               = true
    cache_policy_id        = aws_cloudfront_cache_policy.spa_static_assets.id
  }

  custom_error_response {
    error_code            = 403
    response_code         = 200
    response_page_path    = "/index.html"
    error_caching_min_ttl = 0
  }

  custom_error_response {
    error_code            = 404
    response_code         = 200
    response_page_path    = "/index.html"
    error_caching_min_ttl = 0
  }

  restrictions {
    geo_restriction {
      restriction_type = "none"
    }
  }

  viewer_certificate {
    cloudfront_default_certificate = true
  }

  tags = local.spa_tags
}

data "aws_iam_policy_document" "spa_bucket_policy" {
  statement {
    sid    = "AllowCloudFrontReadOnly"
    effect = "Allow"

    principals {
      type        = "Service"
      identifiers = ["cloudfront.amazonaws.com"]
    }

    actions   = ["s3:GetObject"]
    resources = ["${aws_s3_bucket.spa.arn}/*"]

    condition {
      test     = "StringEquals"
      variable = "AWS:SourceArn"
      values   = [aws_cloudfront_distribution.spa.arn]
    }
  }
}

resource "aws_s3_bucket_policy" "spa" {
  bucket = aws_s3_bucket.spa.id
  policy = data.aws_iam_policy_document.spa_bucket_policy.json

  depends_on = [aws_s3_bucket_public_access_block.spa]
}

resource "null_resource" "spa_build_and_deploy" {
  triggers = {
    source_hash  = local.spa_source_hash
    runtime_hash = local.spa_runtime_hash
  }

  provisioner "local-exec" {
    command     = <<-EOT
      set -euo pipefail
      DIST_DIR="dist"

      VITE_COGNITO_USER_POOL_ID="${module.cognito.user_pool_id}" \
      VITE_COGNITO_APP_CLIENT_ID="${module.cognito.app_client_id}" \
      VITE_COGNITO_DOMAIN="${module.cognito.user_pool_domain}.auth.${var.aws_region}.amazoncognito.com" \
      VITE_COGNITO_REDIRECT_SIGN_IN="https://${aws_cloudfront_distribution.spa.domain_name}/callback" \
      VITE_COGNITO_REDIRECT_SIGN_OUT="https://${aws_cloudfront_distribution.spa.domain_name}" \
      VITE_API_BASE_URL="${local.backend_api_base_url}" \
      npm run build

      aws s3 sync "$DIST_DIR" "s3://${aws_s3_bucket.spa.bucket}" \
        --delete \
        --exclude "assets/*" \
        --cache-control "public,max-age=300,must-revalidate"

      aws s3 sync "$DIST_DIR/assets" "s3://${aws_s3_bucket.spa.bucket}/assets" \
        --delete \
        --cache-control "public,max-age=31536000,immutable"

      aws s3 cp "$DIST_DIR/index.html" "s3://${aws_s3_bucket.spa.bucket}/index.html" \
        --cache-control "no-store,max-age=0" \
        --content-type "text/html"

      aws cloudfront create-invalidation \
        --distribution-id "${aws_cloudfront_distribution.spa.id}" \
        --paths "/*" >/dev/null
    EOT
    interpreter = ["bash", "-c"]
    working_dir = local.spa_source_dir
  }

  depends_on = [
    aws_s3_bucket_policy.spa,
    aws_cloudfront_distribution.spa,
    module.cognito,
    module.backend_ecs
  ]
}

output "spa_bucket_name" {
  description = "SPA hosting bucket name"
  value       = aws_s3_bucket.spa.bucket
}

output "spa_cloudfront_distribution_id" {
  description = "CloudFront distribution ID for SPA"
  value       = aws_cloudfront_distribution.spa.id
}

output "spa_cloudfront_domain_name" {
  description = "CloudFront domain name for SPA"
  value       = aws_cloudfront_distribution.spa.domain_name
}

output "spa_cloudfront_url" {
  description = "CloudFront HTTPS URL for SPA"
  value       = "https://${aws_cloudfront_distribution.spa.domain_name}"
}
