# Zero Trust Data Exchange - Terraform Configuration

This directory contains the Terraform/OpenTofu configuration for deploying the Zero Trust Data Exchange prototype infrastructure on AWS.

## Architecture Overview

The infrastructure deploys two OIDC Identity Providers as AWS Lambda functions with public HTTPS endpoints:

```
IDPs (Lambda Functions with Function URLs)
├── IDP-A (nodejs-express)
└── IDP-B (nodejs-express)

Shared Resources:
├── IAM Role (Lambda execution)
└── CloudWatch Logs
```

## Directory Structure

```
infrastructure/
├── main.tf                # Main configuration (provider, root resources)
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

| Output                | Description                    |
| --------------------- | ------------------------------ |
| `environment`         | Deployment environment         |
| `region`              | AWS region                     |
| `idp_a_function_name` | Lambda function name for IDP-A |
| `idp_a_function_url`  | Public HTTPS URL for IDP-A     |
| `idp_b_function_name` | Lambda function name for IDP-B |
| `idp_b_function_url`  | Public HTTPS URL for IDP-B     |

## State Management

### Local State (Default)

Terraform state is stored locally in `terraform.tfstate`. This is suitable for development but **not recommended for production**.

### Remote State (S3)

To enable remote state, uncomment and configure the backend in `main.tf`:

```hcl
backend "s3" {
  bucket         = "your-terraform-state-bucket"
  key            = "zero-trust-prototype/terraform.tfstate"
  region         = "us-east-1"
  encrypt        = true
  dynamodb_table = "terraform-state-lock"
}
```

Then reinitialize:

```bash
terraform init
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

After deploying the IDPs:

1. Test the OIDC endpoints using the Function URLs
2. Configure AWS Cognito to use these IDPs as identity providers
3. Set up S3 buckets for data storage
4. Configure API Gateway and backend Lambda functions

## References

- [Terraform AWS Provider](https://registry.terraform.io/providers/hashicorp/aws/latest/docs)
- [AWS Lambda Function URLs](https://docs.aws.amazon.com/lambda/latest/dg/lambda-urls.html)
- [OpenTofu Documentation](https://opentofu.org/)

## Support

For issues related to:

- **Terraform syntax**: See [Terraform Documentation](https://www.terraform.io/docs)
- **AWS resources**: See [AWS CLI Documentation](https://docs.aws.amazon.com/cli/)
- **Project specifics**: See [../README.md](../README.md)
