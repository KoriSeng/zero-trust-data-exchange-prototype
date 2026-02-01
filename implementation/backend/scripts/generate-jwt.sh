#!/bin/bash

# Generate test JWT tokens for local testing
# These are NOT cryptographically signed (no signature validation locally)
# Usage: ./generate-jwt.sh [sub] [email] [cognito_group]

# Default values
SUB="${1:-test-user-123@cognito}"
EMAIL="${2:-user@example.com}"
COGNITO_GROUP="${3:-ap-southeast-1_xxxxx_IDP-A}"

# Generate header and payload
HEADER=$(echo -n '{"alg":"none","typ":"JWT"}' | base64 | tr '+/' '-_' | tr -d '=')

PAYLOAD=$(echo -n "{\"sub\":\"$SUB\",\"email\":\"$EMAIL\",\"name\":\"Test User\",\"cognito:groups\":[\"$COGNITO_GROUP\"]}" | base64 | tr '+/' '-_' | tr -d '=')

# Create JWT (no signature, just header.payload.)
JWT="$HEADER.$PAYLOAD."

echo "Generated Test JWT Token:"
echo "=========================="
echo ""
echo "Token: $JWT"
echo ""
echo "Decoded Payload:"
echo "$PAYLOAD" | base64 -d 2>/dev/null | jq . 2>/dev/null || echo "$PAYLOAD" | base64 -d
echo ""
echo "Use with curl:"
echo "curl -H 'Authorization: Bearer $JWT' https://localhost:5001/requests/pending"
echo ""
