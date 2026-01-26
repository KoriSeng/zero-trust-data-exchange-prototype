# Infrastructure README

This directory contains the Infrastructure as Code (IaC) configuration for the Zero Trust Data Exchange prototype, supporting **REQ-013** (Infrastructure as Code with Terraform).

## Overview

The infrastructure is managed using **Terraform** and **OpenTofu**, providing reproducible and version-controlled infrastructure provisioning.

## Prerequisites

The following tools are pre-installed in the devcontainer:

- Terraform (latest)
- OpenTofu (TOFU)
- AWS CLI
- tflint (Terraform linting)
- terragrunt (optional)

## Directory Structure

```
infrastructure/
├── terraform/           # Terraform/OpenTofu configuration files
│   ├── main.tf         # Main configuration and provider setup
│   ├── variables.tf    # Input variables (to be created)
│   ├── outputs.tf      # Output values (to be created)
│   └── README.md       # This file
└── .terraform.d/       # Terraform plugin cache (gitignored)
```

## Getting Started

### Initialize Terraform

```bash
cd infrastructure/terraform
terraform init
```

### Initialize OpenTofu (TOFU Compliance)

```bash
cd infrastructure/terraform
tofu init
```

### Plan Infrastructure Changes

```bash
# Using Terraform
terraform plan

# Using OpenTofu
tofu plan
```

### Apply Infrastructure Changes

```bash
# Using Terraform
terraform apply

# Using OpenTofu
tofu apply
```

## Configuration

### Environment Variables

Set the following environment variables or create a `terraform.tfvars` file:

```hcl
aws_region   = "us-east-1"
environment  = "dev"
project_name = "zero-trust-prototype"
```

**Important:** Never commit `terraform.tfvars` files containing sensitive data. Use `terraform.tfvars.example` for templates.

### AWS Credentials

Ensure your AWS credentials are configured:

```bash
# Via environment variables
export AWS_ACCESS_KEY_ID="your-access-key"
export AWS_SECRET_ACCESS_KEY="your-secret-key"
export AWS_DEFAULT_REGION="us-east-1"

# Or via AWS CLI
aws configure
```

In the devcontainer, your local `~/.aws` directory is mounted automatically.

## State Management

### Local State (Default)

By default, Terraform stores state locally in `terraform.tfstate`. This is suitable for:
- Individual development
- Testing and prototyping
- Academic projects

**Note:** Local state files are excluded from version control via `.gitignore`.

### Remote State (Production)

For team collaboration or production use, configure remote state:

1. Uncomment the backend configuration in `main.tf`
2. Create an S3 bucket and DynamoDB table for state locking
3. Run `terraform init -migrate-state`

Example backend configuration:

```hcl
backend "s3" {
  bucket         = "your-terraform-state-bucket"
  key            = "zero-trust-prototype/terraform.tfstate"
  region         = "us-east-1"
  encrypt        = true
  dynamodb_table = "terraform-state-lock"
}
```

## TOFU Compliance

This infrastructure setup supports the following requirements:

### REQ-013: Infrastructure as Code with Terraform
- All infrastructure is defined in version-controlled Terraform files
- Environments can be created, updated, and reproduced reliably
- Configuration drift is prevented through declarative IaC

### REQ-004: Administrative Onboarding Check
- Infrastructure supports administrative verification workflows
- IAM policies and roles can be configured for administrative access controls

### REQ-019: OIDC IdP Implementation
- Infrastructure can provision Lambda functions for OIDC IdPs
- Supports deployment of IssuerA and IssuerB implementations

## Security Best Practices

1. **Never commit secrets** - Use AWS Secrets Manager, SSM Parameter Store, or environment variables
2. **Enable encryption** - All S3 buckets, databases, and state files should be encrypted
3. **Least privilege** - IAM roles and policies should follow the principle of least privilege
4. **State file security** - Protect state files as they may contain sensitive data
5. **Review plans** - Always review `terraform plan` output before applying changes

## Linting and Validation

### Terraform Validation

```bash
terraform fmt      # Format files
terraform validate # Validate configuration
```

### TFLint

```bash
tflint             # Lint Terraform files
```

## Common Commands

```bash
# Initialize
terraform init

# Format code
terraform fmt -recursive

# Validate configuration
terraform validate

# Plan changes
terraform plan -out=tfplan

# Apply changes
terraform apply tfplan

# Show current state
terraform show

# List resources
terraform state list

# Destroy infrastructure
terraform destroy
```

## OpenTofu Commands

OpenTofu uses the same commands as Terraform, just replace `terraform` with `tofu`:

```bash
tofu init
tofu plan
tofu apply
```

## Troubleshooting

### Provider Plugin Issues

```bash
# Clear plugin cache
rm -rf .terraform
terraform init
```

### State Lock Issues

```bash
# Force unlock (use with caution)
terraform force-unlock <LOCK_ID>
```

### Version Conflicts

```bash
# Check Terraform version
terraform version

# Check OpenTofu version
tofu version
```

## Contributing

When adding new infrastructure:

1. Follow existing naming conventions
2. Add appropriate tags to all resources
3. Document variables and outputs
4. Test changes in a dev environment first
5. Include requirement traceability in comments (e.g., `# REQ-013`)

## Resources

- [Terraform Documentation](https://www.terraform.io/docs)
- [OpenTofu Documentation](https://opentofu.org/docs)
- [AWS Provider Documentation](https://registry.terraform.io/providers/hashicorp/aws/latest/docs)
- [Terraform Best Practices](https://www.terraform-best-practices.com/)

## Project Requirement Traceability

- **REQ-013**: Infrastructure as Code with Terraform - Implemented via this directory
- **REQ-004**: Administrative Onboarding Check - Supported via IAM and access controls
- **REQ-019**: OIDC IdP Implementation - Lambda deployment configuration (to be added)
