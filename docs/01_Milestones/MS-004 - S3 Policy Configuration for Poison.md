---
type: Milestone
milestone_id: MS-004
milestone_name: "S3 Policy Configuration for Poison"
milestone_type: "Configuration"
status: "Complete"
target_date: 2026-02-15
start_date: 2026-02-09
completion_date: 2026-01-26
related_decisions: [DR-001]
related_tasks: [TASK-007]
---

## Objective

Configure the S3 policy for the poison, ensuring that if there is metadata, downloads are not allowed. This will include a test where a file is placed, metadata is set, and a smoke test is conducted to generate a presigned URL to verify that the file cannot be downloaded.

## Deliverables

- ✅ S3 policy configured to restrict downloads based on metadata
- ✅ Test file with metadata set (poison=true tag)
- ✅ Smoke test results verifying download restrictions
- ✅ Automated smoke test script for validation
- ✅ Updated findings summary and final investigation/report inputs

## Implementation

### S3 Bucket Policy

The S3 bucket policy implements tag-based access control using the `aws:PrincipalArn` condition:

```terraform
# Deny s3:GetObject for objects tagged with poison=true
# Exempts the deployer user from this restriction
data "aws_iam_policy_document" "poc_deny_poison" {
  statement {
    sid    = "DenyPoisonedDownload"
    effect = "Deny"
    principals {
      type        = "AWS"
      identifiers = ["*"]
    }
    actions = ["s3:GetObject"]
    resources = [
      "${aws_s3_bucket.poc.arn}/*"
    ]
    condition {
      test     = "StringEquals"
      variable = "s3:ExistingObjectTag/poison"
      values   = ["true"]
    }
    condition {
      test     = "StringNotLike"
      variable = "aws:PrincipalArn"
      values   = ["${var.exempt_user_arn}"]
    }
  }
}
```

**Key Features**:

- Denies `s3:GetObject` for all objects with `poison=true` tag
- Exempts deployer user ARN from restrictions (for management operations)
- Applies to all principals, including pre-signed URL consumers
- Pre-signed URLs respect the policy at download time

### Test Objects

The S3 bucket is pre-seeded with test objects:

1. **safe.txt** (no tags)
   - Available for download by all users
   - Demonstrates normal S3 access

2. **poisoned.txt** (poison=true tag)
   - Blocked for download by presigner role
   - Demonstrates tag-based access denial

### Testing

#### Automated Smoke Test

**Script**: `infrastructure/scripts/s3_smoke.sh`

**Command**:

```bash
cd infrastructure/scripts
./s3_smoke.sh \
  -b "$(cd ../terraform && tofu output -raw poc_bucket_name)" \
  -r "$(cd ../terraform && tofu output -raw poc_presigner_role_arn)" \
  -R "ap-southeast-1"
```

**Test Results**:

The smoke test validates:

1. **Safe Object Download**
   - Assumes presigner role
   - Generates presigned URL for `safe.txt`
   - Downloads with curl: **HTTP 200 ✅**
   - Verifies poison policy does not block untagged objects

2. **Poisoned Object Denial**
   - Assumes presigner role
   - Generates presigned URL for `poisoned.txt` (tagged with poison=true)
   - Download attempt: **HTTP 403 Forbidden ✅**
   - Verifies poison tag effectively blocks access

#### Expected Output

```
==========================================
S3 POC Access Control Smoke Test
==========================================

Testing safe object download...
  Status: 200 ✓ PASSED
  Safe object is downloadable

Testing poisoned object...
  Status: 403 ✓ PASSED
  Poisoned object download denied

==========================================
✓ All tests PASSED
==========================================
```

## Key Findings

### Access Control Enforcement

- **Pre-signed URLs respect bucket policies**: The poison-tag deny policy is enforced at download time, even for pre-signed URLs
- **Principal-level exemptions work**: Using `aws:PrincipalArn` with `StringNotLike` condition successfully exempts the deployer user
- **Fine-grained tag-based control**: Objects can be marked as "poisoned" via tags without code changes

### Design Decisions

1. **Tag-based over path-based**: Using object tags instead of key prefixes allows flexible marking of restricted objects
2. **Presigner role pattern**: Separates concerns between deployer (management) and presigner (URL generation) roles
3. **GET-only restriction**: Policy targets `s3:GetObject` only; other operations (PUT, DELETE) handled separately

## Implications for Zero Trust

This implementation demonstrates:

- **Attribute-based access control (ABAC)**: Using object tags as attributes for access decisions
- **Least privilege**: Presigner role only has permission to generate URLs, not directly access objects
- **Immutable audit trail**: Tag changes are logged in CloudTrail
- **Dynamic policy enforcement**: Poison tags can be applied/removed without redeploying infrastructure

## Next Steps

- [ ] Integrate with approval workflow (MS-005) - API approvals mark files as poison
- [ ] Add audit logging for poison tag changes (MS-006)
- [ ] Build SPA to visualize object access restrictions (MS-009)
