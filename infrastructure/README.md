# Zero Trust Data Exchange - Terraform Configuration

This directory contains the Terraform/OpenTofu configuration for deploying the Zero Trust Data Exchange prototype infrastructure on AWS.

## Architecture Overview

The infrastructure deploys:

- two OIDC Identity Providers as AWS Lambda functions with public HTTPS endpoints
- Cognito with federated IdP configuration for Hosted UI sign-in
- backend API on ECS/Fargate + ALB + DocumentDB
- static SPA hosting with private S3 origin + CloudFront distribution

```
Identity & API
├── IDP-A (Lambda Function URL)
├── IDP-B (Lambda Function URL)
├── Cognito User Pool + App Client
└── Backend ECS + ALB

Data & Frontend
├── DocumentDB (backend persistence)
├── S3 PoC data bucket
└── SPA hosting (S3 + CloudFront OAC)
```

## Directory Structure

```
infrastructure/
├── main.tf                # Main configuration (provider, root modules)
├── spa.tf                 # SPA static hosting resources (S3 + CloudFront)
├── variables.tf           # Input variables
├── outputs.tf             # Output values
├── README.md              # This file
└── modules/
    └── oidc_idp/          # Reusable OIDC IDP module
        ├── main.tf        # Module resources
        ├── variables.tf   # Module input variables
        ├── outputs.tf     # Module output values
        └── README.md      # Module documentation
```

## Prerequisites

### Tools Required

- Terraform >= 1.0 or OpenTofu
- AWS CLI
- AWS account with appropriate permissions

### AWS Permissions Required

- Lambda functions (create, update, delete)
- IAM roles and policies (create, attach)
- CloudWatch Logs (create log groups)
- Lambda Layer operations

### Node.js Dependencies

Before deploying, ensure the IDP source directories have `node_modules` installed:

```bash
cd ../implementation/idp-a
npm install

cd ../implementation/idp-b
npm install
```

## Quick Start

### Initialize Terraform

```bash
cd infrastructure

# Using Terraform
terraform init

# Using OpenTofu (TOFU compliance)
tofu init
```

### Plan Infrastructure

```bash
# Using Terraform
terraform plan -out=tfplan

# Using OpenTofu
tofu plan -out=tfplan
```

### Apply Configuration

```bash
# Using Terraform
terraform apply tfplan

# Using OpenTofu
tofu apply tfplan
```

### View Outputs

After successful deployment, retrieve the Function URLs:

```bash
terraform output

# Or get specific values:
terraform output idp_a_function_url
terraform output idp_b_function_url
```

## Configuration Variables

### Root Variables

Create a `terraform.tfvars` file or pass via command line:

```hcl
aws_region   = "us-east-1"
environment  = "dev"
project_name = "zero-trust-prototype"
```

**Never commit `terraform.tfvars` with sensitive data!**

### Audit logging configuration

The prototype can emit richer audit telemetry to CloudWatch:

- **Step Functions execution logs** (in-stack approval workflow only) with configurable execution data inclusion.
- **S3 object data events** captured by CloudTrail and delivered to CloudWatch Logs.

Controls (see `terraform.tfvars.example`):

- `enable_sfn_execution_logging` (default `true`)
- `sfn_include_execution_data` (default `true`)
- `enable_s3_data_events_cloudtrail` (default `true`)

### OTP debug note

For this PoC flow, OTP delivery is simulated. The backend returns a debug email preview payload
to the SPA instead of sending external emails, so real inboxes are not required for demo users.

### Environment Variables (AWS)

```bash
export AWS_ACCESS_KEY_ID="your-key-id"
export AWS_SECRET_ACCESS_KEY="your-secret-key"
export AWS_DEFAULT_REGION="us-east-1"
```

## Module Usage

The `oidc_idp` module is reusable for deploying additional IDPs. Example:

```hcl
module "idp_c" {
  source = "./modules/oidc_idp"

  idp_name             = "c"
  idp_display_name     = "Issuer C"
  idp_user_set         = "C"
  project_name         = var.project_name
  source_directory     = "${path.module}/../../implementation/idp-c"
  lambda_exec_role_arn = aws_iam_role.lambda_exec_role.arn

  lambda_runtime     = "nodejs.20.x"
  lambda_timeout     = 30
  lambda_memory_size = 512

  tags = {
    Name   = "OIDC-IDP-C"
    Issuer = "C"
  }

  depends_on = [aws_iam_role_policy_attachment.lambda_basic_execution]
}
```

For detailed module documentation, see [modules/oidc_idp/README.md](modules/oidc_idp/README.md).

## Outputs

After deployment, the following outputs are available:

| Output                           | Description                               |
| -------------------------------- | ----------------------------------------- |
| `environment`                    | Deployment environment                    |
| `aws_region`                     | AWS region                                |
| `idp_a_function_name`            | Lambda function name for IDP-A            |
| `idp_a_function_url`             | Public HTTPS URL for IDP-A                |
| `idp_b_function_name`            | Lambda function name for IDP-B            |
| `idp_b_function_url`             | Public HTTPS URL for IDP-B                |
| `backend_api_url`                | API Gateway invoke URL for backend        |
| `cognito_hosted_ui_url`          | Cognito hosted UI base URL                |
| `spa_bucket_name`                | S3 bucket name for SPA assets             |
| `spa_cloudfront_distribution_id` | CloudFront distribution ID for invalidation |
| `spa_cloudfront_url`             | Public CloudFront URL for SPA             |

## State Management

### Local State (Default)

Terraform state is stored locally in `terraform.tfstate`. This is suitable for development but **not recommended for production**.

### Remote State (S3 Only)

This repository is configured with a **partial backend** (`backend "s3" {}` in `main.tf`).
Set concrete backend values in a local `backend.hcl` file.

1. Create backend config from example:

```bash
cp backend.hcl.example backend.hcl
```

2. Edit `backend.hcl` with your real values (`bucket`, `key`, `region`).

3. Ensure LocalStack endpoint overrides are not set:

```bash
unset AWS_ENDPOINT_URL AWS_ENDPOINT_URL_S3 AWS_ENDPOINT_URL_STS
```

4. Initialize and migrate existing local state to S3:

```bash
tofu init -reconfigure -backend-config backend.hcl
```

5. Verify backend and state:

```bash
tofu state list
tofu plan
```

If this is your first backend setup, create the S3 bucket first.
This repository includes a CloudFormation bootstrap template at:

- `infrastructure/bootstrap/terraform-state-bootstrap.yaml`

Example:

```bash
aws cloudformation deploy \
  --stack-name zero-trust-tf-backend \
  --template-file infrastructure/bootstrap/terraform-state-bootstrap.yaml \
  --parameter-overrides StateBucketName=<globally-unique-bucket-name> StatePrefix=zero-trust-prototype/dev \
  --region ap-southeast-1
```

### State Drift Recovery (Existing AWS Resources)

If OpenTofu tries to recreate an existing resource (for example IAM role already exists), import it into state:

```bash
tofu import aws_iam_role.lambda_exec_role zero-trust-prototype-lambda-exec-role
tofu import aws_iam_role_policy_attachment.lambda_basic_execution 'zero-trust-prototype-lambda-exec-role/arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole'
tofu plan
```

## Maintenance

### Updating Infrastructure

To update any configuration:

1. Modify the `.tf` files
2. Run `terraform plan` to review changes
3. Run `terraform apply` to deploy changes

### Destroying Infrastructure

⚠️ **Warning**: This will delete all deployed resources.

```bash
terraform destroy
```

## Validation and Linting

### Validate Configuration

```bash
terraform validate
```

### Format Code

```bash
terraform fmt -recursive
```

### Lint with TFLint

```bash
tflint --init
tflint
```

## Troubleshooting

### Archive Provider Issues

If you encounter issues with the `archive` provider:

```bash
terraform init -upgrade
```

### Lambda Layer Dependencies

Ensure `node_modules` is properly installed in IDP source directories:

```bash
cd ../../implementation/idp-a
npm install --production  # Only production dependencies
```

### Permission Denied Errors

Verify AWS credentials are properly configured:

```bash
aws sts get-caller-identity
```

## Next Steps

After deploying infrastructure:

1. `tofu apply` automatically builds and uploads the SPA with Terraform-derived environment values.
2. Terraform triggers a CloudFront invalidation after SPA upload.
3. Validate end-to-end login/approval/redeem flow through the CloudFront URL.

## References

- [Terraform AWS Provider](https://registry.terraform.io/providers/hashicorp/aws/latest/docs)
- [AWS Lambda Function URLs](https://docs.aws.amazon.com/lambda/latest/dg/lambda-urls.html)
- [OpenTofu Documentation](https://opentofu.org/)

## Support

For issues related to:

- **Terraform syntax**: See [Terraform Documentation](https://www.terraform.io/docs)
- **AWS resources**: See [AWS CLI Documentation](https://docs.aws.amazon.com/cli/)
- **Project specifics**: See [../README.md](../README.md)
