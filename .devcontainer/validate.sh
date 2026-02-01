#!/bin/bash

# Validation script to test devcontainer setup
# Run this after devcontainer is created to verify installation

# Don't exit on error - we want to collect all results
set +e

echo "========================================"
echo "Devcontainer Validation Tests"
echo "========================================"
echo ""

PASS=0
FAIL=0

# Test function
test_command() {
    local cmd="$1"
    local desc="$2"
    local optional="${3:-false}"
    
    echo -n "Testing $desc... "
    if command -v "$cmd" &> /dev/null; then
        echo "✓ PASS"
        ((PASS++))
        return 0
    else
        if [ "$optional" = "true" ]; then
            echo "⊘ SKIP (optional)"
        else
            echo "✗ FAIL"
            ((FAIL++))
        fi
        return 1
    fi
}

# Test version function
test_version() {
    local cmd="$1"
    local desc="$2"
    local optional="${3:-false}"
    
    echo -n "Getting $desc version... "
    if command -v "$cmd" &> /dev/null; then
        local version=$("$cmd" --version 2>&1 | head -1)
        echo "✓ $version"
        ((PASS++))
        return 0
    else
        if [ "$optional" = "true" ]; then
            echo "⊘ SKIP (optional)"
        else
            echo "✗ FAIL"
            ((FAIL++))
        fi
        return 1
    fi
}

# Test file existence
test_file() {
    local file="$1"
    local desc="$2"
    
    echo -n "Checking $desc... "
    if [ -f "$file" ] || [ -d "$file" ]; then
        echo "✓ EXISTS"
        ((PASS++))
        return 0
    else
        echo "✗ MISSING"
        ((FAIL++))
        return 1
    fi
}

echo "Core Tools:"
echo "----------"
test_command node "Node.js"
test_command npm "npm"
test_command git "Git"
test_command curl "curl"
test_command bash "bash"

echo ""
echo "Infrastructure Tools:"
echo "--------------------"
test_command terraform "Terraform"
test_command tofu "OpenTofu"
test_command aws "AWS CLI"
test_command k6 "k6"

echo ""
echo "Optional Tools:"
echo "--------------"
test_command dotnet ".NET SDK" true
test_command tflint "tflint" true
test_command terragrunt "terragrunt" true

echo ""
echo "Tool Versions:"
echo "-------------"
test_version node "Node.js"
test_version npm "npm"
test_version terraform "Terraform"
test_version tofu "OpenTofu"
test_version aws "AWS CLI"
test_version k6 "k6"
test_version dotnet ".NET SDK" true

echo ""
echo "Project Structure:"
echo "-----------------"
test_file "package.json" "Root package.json"
test_file "implementation/idp-a/package.json" "IDP A package.json"
test_file "implementation/idp-b/package.json" "IDP B package.json"
test_file ".devcontainer/devcontainer.json" "Devcontainer config"
test_file ".devcontainer/setup.sh" "Setup script"
test_file "infrastructure/main.tf" "Terraform config"
test_file ".env.example" "Environment template"

echo ""
echo "Node.js Dependencies:"
echo "--------------------"
if [ -d "node_modules" ]; then
    echo "✓ Root dependencies installed"
    ((PASS++))
else
    echo "⊘ Root dependencies not installed (optional)"
fi

if [ -d "implementation/idp-a/node_modules" ]; then
    echo "✓ IDP A dependencies installed"
    ((PASS++))
else
    echo "⊘ IDP A dependencies not installed"
fi

if [ -d "implementation/idp-b/node_modules" ]; then
    echo "✓ IDP B dependencies installed"
    ((PASS++))
else
    echo "⊘ IDP B dependencies not installed"
fi

echo ""
echo "Environment Configuration:"
echo "-------------------------"
echo "NODE_ENV: ${NODE_ENV:-not set}"
echo "AWS_DEFAULT_REGION: ${AWS_DEFAULT_REGION:-not set}"
echo "PWD: $(pwd)"
echo "USER: $(whoami)"

echo ""
echo "========================================"
echo "Summary"
echo "========================================"
echo "Passed: $PASS"
echo "Failed: $FAIL"
echo ""

if [ $FAIL -eq 0 ]; then
    echo "✓ All required checks passed!"
    echo "Environment is ready for development."
    exit 0
else
    echo "✗ Some checks failed."
    echo "Review the failures above."
    exit 1
fi
