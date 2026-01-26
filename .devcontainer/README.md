# Development Container Configuration

This directory contains the configuration for GitHub Codespaces and VS Code Dev Containers support for the Zero Trust Data Exchange Prototype project.

## Overview

The devcontainer provides a consistent, reproducible development environment with all necessary tools pre-installed:

- **Node.js** (LTS via NVM) - For the OIDC IdP implementations (IssuerA and IssuerB)
- **.NET SDK 8.0** - For optional C# development
- **Terraform** - For infrastructure provisioning
- **OpenTofu (TOFU)** - Open-source Terraform alternative
- **AWS CLI** - For AWS infrastructure management
- **Git** - Version control with sensible defaults

## Quick Start

### Using GitHub Codespaces

1. Navigate to the repository on GitHub
2. Click the green "Code" button
3. Select "Codespaces" tab
4. Click "Create codespace on main" (or your branch)

The environment will automatically set up and run the `setup.sh` script.

### Using VS Code Dev Containers Locally

1. Install Docker Desktop
2. Install the "Dev Containers" extension in VS Code
3. Open the repository in VS Code
4. Press `F1` and select "Dev Containers: Reopen in Container"

## Configuration Files

- **devcontainer.json** - Main configuration file defining features, extensions, and settings
- **setup.sh** - Post-creation script that installs OpenTofu and project dependencies

## Features Included

### Node.js Development
- Node.js LTS version via NVM
- All project dependencies pre-installed for:
  - `implementation/idp-a` (OIDC IdP A)
  - `implementation/idp-b` (OIDC IdP B)
- Express, jose, cookie-parser, and other dependencies

### C# Development (Optional)
- .NET SDK 8.0
- .NET Runtime 8.0
- ASP.NET Core Runtime 8.0

### Infrastructure as Code
- **Terraform** - Latest version with tflint and terragrunt
- **OpenTofu (TOFU)** - Open-source Terraform alternative
- AWS CLI for AWS resource management

### VS Code Extensions
- ESLint - JavaScript linting
- Prettier - Code formatting
- Terraform - HashiCorp Terraform support
- C# - .NET development support
- Azure Node Pack - Azure integration
- Docker - Container management

## Port Forwarding

The following ports are automatically forwarded:

| Port | Service | Description |
|------|---------|-------------|
| 3000 | IDP A | OIDC Identity Provider A |
| 3001 | IDP B | OIDC Identity Provider B |
| 3002 | Platform API | Main platform API (if applicable) |
| 5000 | C# API | .NET API (HTTP) |
| 5001 | C# API | .NET API (HTTPS) |

## Environment Variables

The following environment variables are pre-configured:

- `NODE_ENV=development` - Node.js environment
- `AWS_DEFAULT_REGION=us-east-1` - Default AWS region

## AWS Credentials

The devcontainer is configured to mount your local AWS credentials (`~/.aws`) into the container, allowing you to use your existing AWS configuration.

**Note:** For security, ensure your AWS credentials are properly configured with appropriate permissions and never commit them to source control.

## Quick Commands

Once the environment is ready, you can use these commands:

```bash
# Start OIDC Identity Providers
npm run idp-a:dev        # Start IDP A on port 3000
npm run idp-b:dev        # Start IDP B on port 3001

# Run tests
npm run idp:test:smoke   # Run k6 smoke tests

# Terraform operations (when infrastructure code is added)
cd infrastructure/terraform
terraform init           # Initialize Terraform
terraform plan           # Preview infrastructure changes
terraform apply          # Apply infrastructure changes

# OpenTofu operations (TOFU compliance)
cd infrastructure/terraform
tofu init                # Initialize OpenTofu
tofu plan                # Preview infrastructure changes
tofu apply               # Apply infrastructure changes
```

## TOFU Compliance

This environment supports the following TOFU (Trust on First Use) requirements:

### REQ-004: Administrative Onboarding Check
The environment is configured to support administrative verification processes. Synthetic placeholders for administrative onboarding can be configured via environment variables or configuration files.

### REQ-013: Infrastructure as Code with Terraform
Both Terraform and OpenTofu are installed and ready to use. Infrastructure can be provisioned repeatably from source control with documented inputs.

### REQ-019: Implement Simulated OIDC IdPs in Node.js
Node.js dependencies for both OIDC Identity Providers (IssuerA and IssuerB) are automatically installed, and the servers can be started immediately.

## Customization

### Adding New Node.js Dependencies

Dependencies are automatically installed during setup. To add new dependencies:

1. Add them to the appropriate `package.json` file
2. Rebuild the container or run `npm install` manually

### Adding Terraform Modules

Create your Terraform configuration files in the `infrastructure/terraform` directory. The directory is created automatically during setup.

### Modifying Environment Variables

Edit the `remoteEnv` section in `devcontainer.json` to add or modify environment variables.

## Preventing Configuration Drift

The devcontainer ensures consistent environments across:

- **Different developers** - Everyone uses the same base image and features
- **Local and cloud** - Works identically in VS Code locally and GitHub Codespaces
- **Different operating systems** - Container provides uniform Linux environment

All tools and dependencies are version-pinned or use specific tags to ensure reproducibility.

## Troubleshooting

### Container fails to build
- Ensure Docker is running (for local dev containers)
- Check your internet connection
- Review the build logs for specific errors

### AWS credentials not working
- Verify `~/.aws/credentials` exists locally
- Check that the mount path is correct for your OS
- For Codespaces, configure AWS credentials as secrets

### Node.js dependencies not installed
- Run `bash .devcontainer/setup.sh` manually
- Check individual package.json files for errors

### OpenTofu not found
- Run `tofu version` to verify installation
- Re-run the setup script: `bash .devcontainer/setup.sh`

## Security Considerations

- Never commit secrets, AWS credentials, or sensitive data to source control
- Use environment variables or AWS Secrets Manager for sensitive configuration
- The `.env` files are excluded via `.gitignore` - use `.env.example` for templates
- Terraform state files are excluded from version control

## Contributing

When modifying the devcontainer configuration:

1. Test changes locally first
2. Document any new features or requirements
3. Update this README with relevant information
4. Ensure the setup remains minimal and focused on project needs

## Support

For issues or questions:

1. Check this README for troubleshooting steps
2. Review the project's main README
3. Check the project's documentation in the `docs/` directory

## References

- [VS Code Dev Containers Documentation](https://code.visualstudio.com/docs/devcontainers/containers)
- [GitHub Codespaces Documentation](https://docs.github.com/en/codespaces)
- [Dev Container Features](https://containers.dev/features)
- [Terraform Documentation](https://www.terraform.io/docs)
- [OpenTofu Documentation](https://opentofu.org/docs)
