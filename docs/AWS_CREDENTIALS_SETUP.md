# AWS Credentials Setup for Terraform

This guide shows how to properly configure AWS credentials for Terraform deployment.

## Current Setup

Your project has:

- ✅ `.env` file with AWS credentials
- ✅ `terraform.tfvars` with region and project configuration
- ✅ AWS credentials in the `.env` file

## Method 1: Export from .env (Recommended for Development)

```bash
# In the terminal, from project root:
cd /workspaces/zero-trust-data-exchange-prototype

# Export AWS credentials
export AWS_REGION=$(grep "^AWS_REGION=" .env | cut -d'=' -f2)
export AWS_DEFAULT_REGION=$(grep "^AWS_DEFAULT_REGION=" .env | cut -d'=' -f2)
export AWS_ACCESS_KEY_ID=$(grep "^AWS_ACCESS_KEY_ID=" .env | cut -d'=' -f2)
export AWS_SECRET_ACCESS_KEY=$(grep "^AWS_SECRET_ACCESS_KEY=" .env | cut -d'=' -f2)

# Verify credentials work
aws sts get-caller-identity

# Now run Terraform
cd infrastructure/terraform
terraform init
terraform plan
terraform apply
```

## Method 2: Use AWS CLI Configuration

```bash
# Configure AWS CLI interactively
aws configure

# Enter:
# - AWS Access Key ID: [your key from .env]
# - AWS Secret Access Key: [your secret from .env]
# - Default region: ap-southeast-1
# - Default output format: json

# Then run Terraform (credentials are read automatically)
cd infrastructure/terraform
terraform init
terraform plan
terraform apply
```

## Method 3: Using Environment Variables Directly

```bash
# Set all at once (one-liner)
export AWS_REGION=ap-southeast-1 AWS_DEFAULT_REGION=ap-southeast-1 AWS_ACCESS_KEY_ID=your-key-id AWS_SECRET_ACCESS_KEY=your-secret

# Or directly with Terraform
AWS_REGION=ap-southeast-1 AWS_ACCESS_KEY_ID=your-key-id AWS_SECRET_ACCESS_KEY=your-secret terraform apply
```

## Quick Setup Script

Run this script to automatically load credentials and initialize Terraform:

```bash
bash setup-aws.sh
```

This script:

1. Extracts AWS credentials from `.env`
2. Exports them as environment variables
3. Verifies the connection with `aws sts get-caller-identity`
4. Navigates to the Terraform directory
5. Shows next steps

## Verify Credentials are Loaded

```bash
# Check if AWS credentials are set
echo $AWS_ACCESS_KEY_ID
echo $AWS_SECRET_ACCESS_KEY
echo $AWS_REGION

# Test AWS connection
aws sts get-caller-identity

# Should output something like:
# {
#     "UserId": "AIDA...",
#     "Account": "123456789012",
#     "Arn": "arn:aws:iam::123456789012:user/your-user"
# }
```

## Terraform Workflow with Credentials

Once credentials are configured:

```bash
cd infrastructure/terraform

# Step 1: Initialize (downloads providers and modules)
terraform init

# Step 2: Plan (shows what will be created)
terraform plan -out=tfplan

# Step 3: Apply (deploys the infrastructure)
terraform apply tfplan

# Step 4: View outputs (shows the Function URLs)
terraform output
```

## Credentials Precedence (Terraform/AWS CLI)

AWS CLI and Terraform check credentials in this order:

1. **Environment Variables**

   ```bash
   AWS_ACCESS_KEY_ID
   AWS_SECRET_ACCESS_KEY
   AWS_DEFAULT_REGION
   ```

2. **AWS Credentials File** (~/.aws/credentials)

   ```ini
   [default]
   aws_access_key_id = AKIAT6QKTDYW5TXDQW2V
   aws_secret_access_key = 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
   ```

3. **AWS Config File** (~/.aws/config)

   ```ini
   [default]
   region = ap-southeast-1
   ```

4. **IAM Role** (if running on EC2/Lambda)

## Security Best Practices ⚠️

1. **Never commit `.env` to version control**
   - Check `.gitignore` includes `.env`:

   ```bash
   grep "^\.env" .gitignore
   ```

2. **Keep `.env` local only**
   - Only you should have the credentials file

3. **Rotate credentials regularly**
   - Update keys periodically in AWS Console

4. **Use IAM roles in production**
   - Don't use long-lived access keys in production
   - Use temporary STS credentials instead

5. **Grant minimal permissions**
   - Give the user/role only the permissions needed
   - Required permissions:
     - `lambda:*`
     - `iam:*`
     - `logs:*`

## Troubleshooting

### "Error: error configuring Terraform AWS Provider"

```bash
# Check if credentials are set
printenv | grep AWS

# If empty, load from .env:
export $(cat .env | grep -v '#' | grep AWS | xargs)

# Verify
aws sts get-caller-identity
```

### "InvalidClientTokenId: The security token included in the request is invalid"

- Credentials are expired or incorrect
- Regenerate them in AWS Console

### "AccessDenied: User is not authorized to perform: lambda:CreateFunction"

- User doesn't have proper permissions
- Add required IAM policy

### "Provider error: Required plugin version constraints cannot be satisfied"

```bash
# Reinitialize
terraform init -upgrade
```

## Next Steps

1. Export credentials: `export $(cat .env | grep AWS | xargs)`
2. Verify connection: `aws sts get-caller-identity`
3. Initialize Terraform: `cd infrastructure/terraform && terraform init`
4. Plan deployment: `terraform plan -out=tfplan`
5. Deploy: `terraform apply tfplan`
6. View outputs: `terraform output`

## Support

- Terraform AWS Provider: https://registry.terraform.io/providers/hashicorp/aws/latest/docs
- AWS CLI Documentation: https://docs.aws.amazon.com/cli/
- Terraform Documentation: https://www.terraform.io/docs/
