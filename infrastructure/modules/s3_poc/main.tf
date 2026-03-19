locals {
  resolved_bucket_name = coalesce(var.bucket_name, "${var.project_name}-${var.environment}-poc-data")
  base_tags = merge({
    Name        = "poc-data"
    Environment = var.environment
    Project     = var.project_name
  }, var.tags)
  presigner_role_name = coalesce(var.presigner_role_name, "${var.project_name}-${var.environment}-poc-presigner")
  sample_root         = "${path.module}/sample"
  sample_files        = fileset(local.sample_root, "**/*")
  sample_content_type = {
    txt    = "text/plain"
    csv    = "text/csv"
    tsv    = "text/tab-separated-values"
    json   = "application/json"
    jsonl  = "application/x-ndjson"
    parquet = "application/octet-stream"
    png    = "image/png"
  }
}

resource "aws_s3_bucket" "poc" {
  bucket        = local.resolved_bucket_name
  force_destroy = var.force_destroy

  tags = local.base_tags
}

resource "aws_s3_bucket_public_access_block" "poc" {
  bucket                  = aws_s3_bucket.poc.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

data "aws_iam_policy_document" "poc_deny_poison" {
  statement {
    sid    = "DenyPoisonTaggedObjects"
    effect = "Deny"

    principals {
      type        = "*"
      identifiers = ["*"]
    }

    actions   = ["s3:GetObject"]
    resources = ["${aws_s3_bucket.poc.arn}/*"]

    condition {
      test     = "StringLike"
      variable = "s3:ExistingObjectTag/poison"
      values   = ["*"]
    }

    dynamic "condition" {
      for_each = var.exempt_user_arn == null ? [] : [var.exempt_user_arn]
      content {
        test     = "StringNotLike"
        variable = "aws:PrincipalArn"
        values   = [condition.value]
      }
    }
  }
}

resource "aws_s3_bucket_policy" "poc" {
  bucket = aws_s3_bucket.poc.id
  policy = data.aws_iam_policy_document.poc_deny_poison.json
}

# ---------------------------------------------------------------------------
# Presigner IAM role (assumable by the allowed account) to generate pre-signed URLs
# ---------------------------------------------------------------------------

resource "aws_iam_role" "poc_presigner" {
  count = var.create_presigner_role && var.allow_account_id != null ? 1 : 0

  name = local.presigner_role_name

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Principal = {
          AWS = "arn:aws:iam::${var.allow_account_id}:root"
        }
        Action = "sts:AssumeRole"
      }
    ]
  })

  tags = local.base_tags
}

data "aws_iam_policy_document" "poc_presigner" {
  count = var.create_presigner_role && var.allow_account_id != null ? 1 : 0

  statement {
    effect = "Allow"
    actions = [
      "s3:GetObject"
    ]
    resources = [
      "${aws_s3_bucket.poc.arn}/*"
    ]
  }

  statement {
    effect    = "Allow"
    actions   = ["s3:ListBucket"]
    resources = [aws_s3_bucket.poc.arn]
  }
}

resource "aws_iam_role_policy" "poc_presigner" {
  count = var.create_presigner_role && var.allow_account_id != null ? 1 : 0

  name   = "${local.presigner_role_name}-policy"
  role   = aws_iam_role.poc_presigner[0].id
  policy = data.aws_iam_policy_document.poc_presigner[0].json
}

# ---------------------------------------------------------------------------
# Seed objects for smoke testing
# ---------------------------------------------------------------------------

resource "aws_s3_object" "safe_sample" {
  count        = var.create_seed_objects ? 1 : 0
  bucket       = aws_s3_bucket.poc.id
  key          = "samples/safe.txt"
  content      = "This is a safe sample object."
  content_type = "text/plain"
}

resource "aws_s3_object" "poisoned_sample" {
  count        = var.create_seed_objects ? 1 : 0
  bucket       = aws_s3_bucket.poc.id
  key          = "samples/poisoned.txt"
  content      = "This object is tagged as poison and should be denied for GetObject."
  content_type = "text/plain"

  tags = {
    poison = "true"
  }
}

resource "aws_s3_object" "dataset_samples" {
  for_each = var.create_seed_objects ? local.sample_files : []

  bucket = aws_s3_bucket.poc.id
  key    = each.value
  source = "${local.sample_root}/${each.value}"
  etag   = filemd5("${local.sample_root}/${each.value}")

  content_type = lookup(
    local.sample_content_type,
    lower(element(reverse(split(".", each.value)), 0)),
    "application/octet-stream"
  )
}
