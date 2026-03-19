# Setup (AWS Account Required)

This project now expects a real AWS account for end-to-end infrastructure testing (especially Cognito federation behavior).

## 1) Prerequisites

- AWS account with permissions for CloudFormation, S3, IAM, Lambda, API Gateway, CloudFront, and Cognito
- AWS CLI authenticated (`aws sts get-caller-identity` succeeds)
- OpenTofu installed (`tofu version`)
- Node.js installed (`node --version`)
- .NET SDK 10 installed (`dotnet --version`)
- `zip` utility available in your bash environment (`zip -v`)
- Linux-compatible shell environment required (Linux/macOS, or WSL Ubuntu on Windows)

## 2) Bootstrap remote state backend with CloudFormation

Before running Terraform/OpenTofu in `infrastructure/`, create the backend resources:

```powershell
aws cloudformation deploy `
  --stack-name zero-trust-tf-backend `
  --template-file infrastructure\bootstrap\terraform-state-bootstrap.yaml `
  --parameter-overrides `
    StateBucketName=<globally-unique-bucket-name> `
    StatePrefix=zero-trust-prototype/dev `
  --region ap-southeast-1
```

Then copy `infrastructure\backend.hcl.example` to `infrastructure\backend.hcl` and set:

- `bucket` to your `StateBucketName`
- `region` to your deployment region

## 3) Initialize Terraform/OpenTofu with remote state

```powershell
cd infrastructure
tofu init -reconfigure -backend-config backend.hcl
```

## 4) Deploy infrastructure

```powershell
tofu plan -out=tfplan
tofu apply tfplan
```

`tofu apply` now also builds and deploys the SPA artifact to the Terraform-managed S3/CloudFront frontend using environment values from Terraform outputs.

## 5) One-command cycle scripts (bash)

From WSL/Ubuntu:

```bash
bash scripts/aws-cycle.sh
```

For non-interactive apply:

```bash
bash scripts/aws-cycle.sh --auto-approve
```

To tear everything down (state bucket remains, because it is not managed in this stack):

```bash
bash scripts/aws-destroy.sh
```

Non-interactive destroy:

```bash
bash scripts/aws-destroy.sh --auto-approve
```

## Notes

- LocalStack remains useful for development, but Cognito Hosted UI/IdP callback behavior can differ from AWS.
- Use AWS deployment for final validation of federated login redirects and callback flows.
- PowerShell-only execution is not supported for the infra apply/build workflow; use WSL/Linux shell.
