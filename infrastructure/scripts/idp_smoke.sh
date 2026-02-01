#!/usr/bin/env bash

# Smoke test for OIDC Lambda IdP implementations
# Tests that the Lambda Function URLs are accessible and return proper OIDC metadata

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

IDP_A_URL=""
IDP_B_URL=""

# Parse command line arguments
while getopts "a:b:h" opt; do
    case $opt in
        a) IDP_A_URL="$OPTARG";;
        b) IDP_B_URL="$OPTARG";;
        h)
            echo "Usage: $0 [-a <idp_a_url>] [-b <idp_b_url>]"
            echo "Defaults to terraform outputs idp_a_function_url and idp_b_function_url"
            exit 0
            ;;
        \?) echo "Invalid option -$OPTARG" >&2; exit 1;;
    esac
done

terraform_dir="$(cd "$(dirname "$0")/.." && pwd)"

read_tf_output() {
    local name="$1"
    local TF_CMD="tofu"
    if ! command -v tofu >/dev/null 2>&1; then
        TF_CMD="terraform"
    fi
    (cd "$terraform_dir" && $TF_CMD output -raw "$name")
}

if [[ -z "$IDP_A_URL" ]]; then
    IDP_A_URL=$(read_tf_output idp_a_function_url 2>/dev/null || true)
fi

if [[ -z "$IDP_B_URL" ]]; then
    IDP_B_URL=$(read_tf_output idp_b_function_url 2>/dev/null || true)
fi

# Validate required parameters
if [ -z "$IDP_A_URL" ] || [ -z "$IDP_B_URL" ]; then
    echo -e "${RED}Error: Missing required IdP URLs${NC}"
    echo "Usage: $0 [-a <idp_a_url>] [-b <idp_b_url>]"
    echo "Defaults to terraform outputs idp_a_function_url and idp_b_function_url"
    exit 1
fi

echo "=========================================="
echo "OIDC Lambda IdP Smoke Test"
echo "=========================================="
echo ""

# Function to test an IdP endpoint
test_idp() {
    local idp_name=$1
    local idp_url=$2
    
    echo -e "${YELLOW}Testing ${idp_name}...${NC}"
    echo "URL: ${idp_url}"
    
    # Test 1: OIDC Discovery Document
    echo -n "  Testing OIDC Discovery Document... "
    discovery_url="${idp_url}.well-known/openid-configuration"
    discovery_response=$(curl -s -w "\n%{http_code}" "$discovery_url")
    discovery_status=$(echo "$discovery_response" | tail -n1)
    discovery_body=$(echo "$discovery_response" | sed '$d')
    
    if [ "$discovery_status" -eq 200 ]; then
        # Validate required OIDC fields are present
        if echo "$discovery_body" | jq -e '.issuer' > /dev/null 2>&1 && \
           echo "$discovery_body" | jq -e '.authorization_endpoint' > /dev/null 2>&1 && \
           echo "$discovery_body" | jq -e '.token_endpoint' > /dev/null 2>&1 && \
           echo "$discovery_body" | jq -e '.jwks_uri' > /dev/null 2>&1; then
            echo -e "${GREEN}✓ PASSED${NC} (Status: $discovery_status)"
            echo "    Issuer: $(echo "$discovery_body" | jq -r '.issuer')"
        else
            echo -e "${RED}✗ FAILED${NC} - Missing required OIDC fields"
            echo "$discovery_body" | jq .
            return 1
        fi
    else
        echo -e "${RED}✗ FAILED${NC} (Status: $discovery_status)"
        echo "$discovery_response"
        return 1
    fi
    
    # Test 2: JWKS Endpoint
    echo -n "  Testing JWKS Endpoint... "
    jwks_url="${idp_url}jwks"
    jwks_response=$(curl -s -w "\n%{http_code}" "$jwks_url")
    jwks_status=$(echo "$jwks_response" | tail -n1)
    jwks_body=$(echo "$jwks_response" | sed '$d')
    
    if [ "$jwks_status" -eq 200 ]; then
        # Validate keys array exists
        if echo "$jwks_body" | jq -e '.keys' > /dev/null 2>&1; then
            key_count=$(echo "$jwks_body" | jq '.keys | length')
            echo -e "${GREEN}✓ PASSED${NC} (Status: $jwks_status, Keys: $key_count)"
        else
            echo -e "${RED}✗ FAILED${NC} - Invalid JWKS format"
            echo "$jwks_body" | jq .
            return 1
        fi
    else
        echo -e "${RED}✗ FAILED${NC} (Status: $jwks_status)"
        echo "$jwks_response"
        return 1
    fi
    
    # Test 3: Authorization Endpoint (should be accessible)
    echo -n "  Testing Authorization Endpoint... "
    auth_url="${idp_url}authorize"
    auth_response=$(curl -s -o /dev/null -w "%{http_code}" "$auth_url")
    
    # Accept 302 (redirect), 400 (missing params), or 200 as valid responses
    if [ "$auth_response" -eq 302 ] || [ "$auth_response" -eq 400 ] || [ "$auth_response" -eq 200 ]; then
        echo -e "${GREEN}✓ PASSED${NC} (Status: $auth_response)"
    else
        echo -e "${RED}✗ FAILED${NC} (Status: $auth_response)"
        return 1
    fi
    
    echo ""
    return 0
}

# Test both IdPs
all_passed=true

if ! test_idp "IDP-A" "$IDP_A_URL"; then
    all_passed=false
fi

if ! test_idp "IDP-B" "$IDP_B_URL"; then
    all_passed=false
fi

# Summary
echo "=========================================="
if [ "$all_passed" = true ]; then
    echo -e "${GREEN}✓ All tests PASSED${NC}"
    echo "=========================================="
    exit 0
else
    echo -e "${RED}✗ Some tests FAILED${NC}"
    echo "=========================================="
    exit 1
fi
