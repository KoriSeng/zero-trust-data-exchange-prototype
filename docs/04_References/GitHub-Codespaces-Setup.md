# GitHub Codespaces Development Environment

## Overview

This document describes the GitHub Codespaces configuration for the Zero Trust Data Exchange prototype, supporting consistent development environments across all contributors.

## Purpose

The devcontainer configuration ensures:

1. **Consistency** - All developers use identical tooling and versions
2. **Quick Onboarding** - New contributors can start coding within minutes
3. **Requirement Support** - Pre-configured for project-specific needs
4. **No Configuration Drift** - Environment is reproducible from source control

## Supported Requirements

This environment directly supports the following project requirements:

### REQ-013: Infrastructure as Code with Terraform
- Terraform CLI (latest) pre-installed
- OpenTofu (TOFU) for open-source IaC
- tflint for Terraform code quality
- Terragrunt for advanced workflows
- AWS CLI for infrastructure management

### REQ-004: Administrative Onboarding Check for NDA Verification
- Environment configured to support administrative workflows
- Placeholder configuration for verification endpoints
- Support for audit logging and compliance tracking

### REQ-019: Implement Simulated OIDC IdPs in Node.js
- Node.js LTS via NVM
- All OIDC IdP dependencies (express, jose, cookie-parser) pre-installed
- Both IssuerA and IssuerB ready to start immediately
- Port forwarding configured (3000, 3001)

## Architecture

The devcontainer is built on:

- **Base Image**: Microsoft's official Ubuntu-based devcontainer
- **Features**: Modular components installed via devcontainer features
- **Extensions**: VS Code extensions for development workflow
- **Post-Create**: Automated setup script for environment initialization

## Tools Included

### Core Development Tools

| Tool | Version | Purpose |
|------|---------|---------|
| Node.js | LTS (via NVM) | JavaScript runtime for IdP services |
| npm | Latest | Package manager |
| .NET SDK | 8.0 | C# development (optional) |
| Git | Latest | Version control |

### Infrastructure Tools

| Tool | Version | Purpose |
|------|---------|---------|
| Terraform | Latest | Infrastructure as Code |
| OpenTofu | Latest | Open-source Terraform alternative |
| tflint | Latest | Terraform linting |
| Terragrunt | Latest | Terraform wrapper |
| AWS CLI | Latest | AWS resource management |

### VS Code Extensions

- **ESLint** - JavaScript/Node.js linting
- **Prettier** - Code formatting
- **Terraform** - HashiCorp Terraform support
- **C#** - .NET language support
- **Azure Node Pack** - Azure development tools
- **Docker** - Container management

## Quick Start

### Using GitHub Codespaces

1. Go to the repository on GitHub
2. Click "Code" → "Codespaces"
3. Create or open a codespace
4. Wait for automatic setup (~2-3 minutes)
5. Start developing!

### Using VS Code Locally

1. Install Docker Desktop
2. Install "Dev Containers" extension
3. Open repository in VS Code
4. Press F1 → "Dev Containers: Reopen in Container"

## Configuration Files

### `.devcontainer/devcontainer.json`

Main configuration file that defines:
- Base image and features
- VS Code settings and extensions
- Port forwarding rules
- Environment variables
- Mount points (e.g., AWS credentials)

### `.devcontainer/setup.sh`

Post-creation script that:
- Installs OpenTofu (TOFU)
- Installs all Node.js dependencies
- Configures Git defaults
- Creates infrastructure directories
- Displays quick start information

### `.devcontainer/README.md`

Detailed documentation for devcontainer usage.

## Environment Variables

Pre-configured environment variables:

```bash
NODE_ENV=development
AWS_DEFAULT_REGION=us-east-1
```

Additional variables can be configured in `.env` (see `.env.example`).

## Port Forwarding

Automatically forwarded ports:

| Port | Service | Auto Forward |
|------|---------|--------------|
| 3000 | IDP A | Notify |
| 3001 | IDP B | Notify |
| 3002 | Platform API | Notify |
| 5000 | C# API (HTTP) | Notify |
| 5001 | C# API (HTTPS) | Notify |

## AWS Integration

AWS credentials are mounted from your local machine:

```
~/.aws → /home/vscode/.aws
```

For Codespaces, configure credentials as repository secrets or environment variables.

## Development Workflow

### Starting OIDC Identity Providers

```bash
# Terminal 1 - IDP A
npm run idp-a:dev

# Terminal 2 - IDP B
npm run idp-b:dev
```

### Running Tests

```bash
npm run idp:test:smoke
```

### Infrastructure Management

```bash
cd infrastructure/terraform

# Using Terraform
terraform init
terraform plan
terraform apply

# Using OpenTofu (TOFU)
tofu init
tofu plan
tofu apply
```

## TOFU Compliance Details

### REQ-004: Administrative Onboarding Check

The environment supports administrative verification through:
- Environment variable configuration (`ADMIN_VERIFICATION_ENABLED`)
- Placeholder endpoints for NDA verification
- Audit logging capabilities

Configure in `.env`:
```bash
ADMIN_VERIFICATION_ENABLED=true
NDA_VERIFICATION_ENDPOINT=https://example.com/verify-nda
```

### REQ-013: Infrastructure as Code

Infrastructure reproducibility through:
- Version-controlled Terraform configuration
- Automated tooling installation
- Consistent AWS provider versions
- State management options (local or remote)

### REQ-019: OIDC IdP Implementation

Rapid IdP deployment through:
- Pre-installed Node.js and dependencies
- Express and jose libraries ready
- Mock user data and configurations
- Isolated test environments

## Security Considerations

### Credentials Management

- ✅ **DO**: Use environment variables or AWS Secrets Manager
- ✅ **DO**: Keep `.env` files local (gitignored)
- ✅ **DO**: Use `.env.example` for templates
- ❌ **DON'T**: Commit secrets to source control
- ❌ **DON'T**: Share AWS credentials in code

### Terraform State

- State files may contain sensitive data
- Use remote state with encryption for production
- Local state is excluded from version control
- Review `.gitignore` for state file patterns

### Network Security

- Ports are forwarded but not publicly exposed
- Use HTTPS for production deployments
- Configure security groups appropriately
- Follow least-privilege access principles

## Customization

### Adding Node.js Dependencies

1. Update appropriate `package.json`
2. Rebuild container or run `npm install`

### Adding VS Code Extensions

Edit `devcontainer.json`:
```json
"extensions": [
  "your.extension.id"
]
```

### Modifying Environment Variables

Edit `devcontainer.json`:
```json
"remoteEnv": {
  "YOUR_VAR": "value"
}
```

### Adding System Packages

Edit `setup.sh` or use devcontainer features.

## Troubleshooting

### Container Build Failures

- Check Docker is running (local dev)
- Verify internet connectivity
- Review build logs
- Check feature versions

### Dependency Installation Issues

```bash
# Manually run setup
bash .devcontainer/setup.sh

# Clean npm cache
npm cache clean --force
```

### AWS Credentials Not Working

- Verify `~/.aws/credentials` exists
- Check mount configuration
- For Codespaces, configure secrets

### Port Conflicts

- Check if ports are already in use
- Modify port forwarding in `devcontainer.json`
- Use alternative ports in `.env`

## Performance Optimization

### Faster Builds

- Use pre-built images when possible
- Minimize layers in custom Dockerfiles
- Cache dependencies appropriately

### Resource Management

- Close unused codespaces
- Use smaller machine types for light work
- Enable auto-stop for codespaces

## Best Practices

1. **Keep Configuration Minimal** - Only include necessary tools
2. **Document Changes** - Update README when modifying devcontainer
3. **Test Locally First** - Validate changes before pushing
4. **Version Pin Critical Tools** - Prevent unexpected updates
5. **Secure by Default** - Never commit secrets

## Comparison: Local vs Codespaces

| Aspect | Local Dev Container | GitHub Codespaces |
|--------|---------------------|-------------------|
| Setup | Requires Docker Desktop | Fully cloud-based |
| Performance | Local hardware | Cloud VM (configurable) |
| Collaboration | Manual sharing | URL-based sharing |
| Cost | Free (uses local resources) | Free tier + paid options |
| Internet | Required for initial setup | Required always |

## Support and Resources

### Documentation

- [Devcontainer README](.devcontainer/README.md)
- [Infrastructure README](infrastructure/README.md)
- [Project Documentation](../README.md)

### External Resources

- [VS Code Dev Containers](https://code.visualstudio.com/docs/devcontainers/containers)
- [GitHub Codespaces Docs](https://docs.github.com/en/codespaces)
- [Devcontainer Features](https://containers.dev/features)

## Maintenance

### Updating Tool Versions

Edit `devcontainer.json` feature versions:
```json
"ghcr.io/devcontainers/features/node:1": {
  "version": "20"  // Update as needed
}
```

### Regular Updates

- Review feature updates quarterly
- Test compatibility with new versions
- Update documentation accordingly

## Contributing

When modifying the devcontainer:

1. Test changes locally first
2. Document modifications in README
3. Update this guide if needed
4. Consider backward compatibility

## Changelog

### Initial Setup (2026-01-26)

- Created devcontainer configuration
- Installed Node.js, .NET SDK, Terraform, OpenTofu
- Configured VS Code extensions and settings
- Added automated setup script
- Documented TOFU compliance provisions

## Related Requirements

- REQ-004: Administrative Onboarding Check for NDA Verification
- REQ-013: Infrastructure as Code with Terraform
- REQ-019: Implement Simulated OIDC IdPs in Node.js

## Project Context

This environment configuration supports the academic prototype for investigating Zero Trust Architectures for secure inter-organizational data exchange. It is designed for educational use and demonstration purposes only.
