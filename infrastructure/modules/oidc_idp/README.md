# OIDC IDP Module

This module deploys an OIDC Identity Provider as an AWS Lambda function with a public HTTPS Function URL.

## Features

- Deploys Node.js Express application as Lambda function
- Automatically packages dependencies as Lambda layer
- Creates public Function URL with CORS support
- Configurable environment variables per IDP
- Reusable for multiple IDP instances

## Usage

```hcl
module "idp_a" {
  source = "./modules/oidc_idp"

  idp_name         = "a"
  idp_display_name = "Issuer A"
  idp_user_set     = "A"
  project_name     = var.project_name
  source_directory = path.module/../../implementation/idp-a"
  lambda_exec_role_arn = aws_iam_role.lambda_exec_role.arn

  lambda_runtime    = "nodejs.20.x"
  lambda_timeout    = 30
  lambda_memory_size = 512

  tags = {
    Name   = "OIDC-IDP-A"
    Issuer = "A"
  }
}
```

## Inputs

| Name                   | Description                                  | Type         | Default            | Required |
| ---------------------- | -------------------------------------------- | ------------ | ------------------ | -------- |
| `idp_name`             | Name identifier for the IDP (e.g., 'a', 'b') | string       | -                  | yes      |
| `idp_display_name`     | Display name for the IDP (e.g., 'Issuer A')  | string       | -                  | yes      |
| `idp_user_set`         | User set for the IDP (e.g., 'A', 'B')        | string       | -                  | yes      |
| `project_name`         | Project name for resource naming             | string       | -                  | yes      |
| `source_directory`     | Path to the IDP source code directory        | string       | -                  | yes      |
| `lambda_exec_role_arn` | ARN of the Lambda execution role             | string       | -                  | yes      |
| `lambda_runtime`       | Lambda runtime                               | string       | `nodejs.20.x`      | no       |
| `lambda_timeout`       | Lambda function timeout in seconds           | number       | `30`               | no       |
| `lambda_memory_size`   | Lambda function memory size in MB            | number       | `512`              | no       |
| `cors_origins`         | CORS allowed origins for Function URL        | list(string) | `["*"]`            | no       |
| `cors_methods`         | CORS allowed methods for Function URL        | list(string) | `["*"]`            | no       |
| `cors_headers`         | CORS allowed headers for Function URL        | list(string) | `["content-type"]` | no       |
| `authorization_type`   | Authorization type for Function URL          | string       | `NONE`             | no       |
| `tags`                 | Additional tags for resources                | map(string)  | `{}`               | no       |

## Outputs

| Name                   | Description                               |
| ---------------------- | ----------------------------------------- |
| `lambda_function_arn`  | ARN of the Lambda function                |
| `lambda_function_name` | Name of the Lambda function               |
| `function_url`         | Function URL for the IDP                  |
| `function_url_id`      | Function URL ID                           |
| `lambda_layer_arn`     | ARN of the Lambda layer with dependencies |

## Requirements

- The IDP source directory must contain:
  - `handler.js` - Exports the Express app as the handler
  - `package.json` - Node.js dependencies
  - `node_modules/` - Installed dependencies (or run `npm install` before Terraform)

## Environment Variables

The Lambda function sets the following environment variables:

- `ISSUER_NAME` - Set from `idp_display_name`
- `USER_SET` - Set from `idp_user_set`
- `PORT` - Always set to `8080`
