#!/bin/bash

# Setup script for GitHub Codespaces
# This script runs after the devcontainer is created

set -e

echo "=========================================="
echo "Zero Trust Data Exchange - Environment Setup"
echo "=========================================="

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print section headers
print_section() {
    echo ""
    echo -e "${BLUE}==>${NC} $1"
}

# Install OpenTofu (TOFU)
print_section "Installing OpenTofu (TOFU)..."
if ! command -v tofu &> /dev/null; then
    # Install OpenTofu
    # Fetch the latest release info once
    TOFU_RELEASE=$(curl -s https://api.github.com/repos/opentofu/opentofu/releases/latest)
    TOFU_VERSION=$(echo "$TOFU_RELEASE" | grep -o '"tag_name": "[^"]*' | cut -d'"' -f4 | sed 's/v//')
    
    if [ -z "$TOFU_VERSION" ]; then
        echo "Error: Failed to fetch OpenTofu version"
        exit 1
    fi
    
    curl -Lo tofu.deb "https://github.com/opentofu/opentofu/releases/latest/download/tofu_${TOFU_VERSION}_amd64.deb"
    sudo dpkg -i tofu.deb
    rm tofu.deb
    echo -e "${GREEN}✓${NC} OpenTofu installed successfully"
else
    echo -e "${GREEN}✓${NC} OpenTofu already installed"
fi

# Verify OpenTofu installation
tofu version

# Install project dependencies
print_section "Installing Node.js project dependencies..."

# Root level dependencies
if [ -f "package.json" ]; then
    echo "Installing root dependencies..."
    npm install
fi

# IDP A dependencies
if [ -f "implementation/idp-a/package.json" ]; then
    echo "Installing IDP A dependencies..."
    cd implementation/idp-a
    npm install
    cd ../..
fi

# IDP B dependencies
if [ -f "implementation/idp-b/package.json" ]; then
    echo "Installing IDP B dependencies..."
    cd implementation/idp-b
    npm install
    cd ../..
fi

echo -e "${GREEN}✓${NC} All Node.js dependencies installed"

# Configure Git
print_section "Configuring Git..."
git config --global init.defaultBranch main
git config --global core.autocrlf input
echo -e "${GREEN}✓${NC} Git configured"

# Create placeholder directories for Terraform state (if needed)
print_section "Setting up Terraform/TOFU directories..."
mkdir -p infrastructure/terraform
mkdir -p infrastructure/.terraform.d
echo -e "${GREEN}✓${NC} Infrastructure directories created"

# Display environment information
print_section "Environment Information:"
echo "Node.js version: $(node --version)"
echo "npm version: $(npm --version)"
echo "Terraform version: $(terraform version -json | grep -o '"terraform_version":"[^"]*' | cut -d'"' -f4)"
echo "OpenTofu version: $(tofu version -json | grep -o '"terraform_version":"[^"]*' | cut -d'"' -f4)"

if command -v dotnet &> /dev/null; then
    echo ".NET SDK version: $(dotnet --version)"
fi

echo ""
print_section "TOFU Compliance Notes:"
echo "- REQ-004: Administrative Onboarding Check - Supported via environment setup"
echo "- REQ-013: Infrastructure as Code - Terraform and OpenTofu installed"
echo "- REQ-019: OIDC IdP Implementation - Node.js dependencies installed"
echo ""
echo -e "${GREEN}✓${NC} Environment setup for TOFU requirements complete"

# Display quick start information
echo ""
echo "=========================================="
echo "Quick Start Commands:"
echo "=========================================="
echo "Start IDP A:  npm run idp-a:dev"
echo "Start IDP B:  npm run idp-b:dev"
echo "Run tests:    npm run idp:test:smoke"
echo ""
echo "Terraform:    cd infrastructure/terraform && terraform init"
echo "OpenTofu:     cd infrastructure/terraform && tofu init"
echo "=========================================="
echo -e "${GREEN}Environment is ready!${NC}"
echo "=========================================="
