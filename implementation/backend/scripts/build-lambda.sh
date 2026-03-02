#!/bin/bash
# Build script for .NET 10 Lambda deployment package
# Produces a framework-dependent zip for the dotnet10 managed Lambda runtime
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_DIR="$SCRIPT_DIR/.."
REPO_ROOT="$SCRIPT_DIR/../../.."
OUTPUT_DIR="$REPO_ROOT/build/lambda-backend"
ZIP_PATH="$REPO_ROOT/build/backend-lambda.zip"

echo "=== Building ZeroTrust.Backend Lambda package ==="
echo "Backend:  $BACKEND_DIR"
echo "Output:   $OUTPUT_DIR"
echo "Zip:      $ZIP_PATH"
echo ""

# Create output directory
mkdir -p "$(dirname "$ZIP_PATH")"
rm -rf "$OUTPUT_DIR"

# Framework-dependent publish — the dotnet10 managed runtime provides the .NET runtime
dotnet publish "$BACKEND_DIR" \
  -c Release \
  -o "$OUTPUT_DIR"

# Download the Amazon RDS / DocumentDB CA bundle for TLS connections
# This certificate is needed for MongoDB Driver TLS connections to DocumentDB
CA_BUNDLE_URL="https://truststore.pki.rds.amazonaws.com/global/global-bundle.pem"
CA_BUNDLE_PATH="$OUTPUT_DIR/rds-combined-ca-bundle.pem"
echo "Downloading RDS CA bundle..."
if command -v curl &> /dev/null; then
  curl -sL "$CA_BUNDLE_URL" -o "$CA_BUNDLE_PATH"
else
  wget -q "$CA_BUNDLE_URL" -O "$CA_BUNDLE_PATH"
fi
echo "CA bundle saved: $CA_BUNDLE_PATH"

# Create deployment zip
rm -f "$ZIP_PATH"
echo "Creating zip package..."
cd "$OUTPUT_DIR"
zip -q -r "$ZIP_PATH" .
cd -

echo ""
echo "=== Build complete ==="
echo "Package: $ZIP_PATH ($(du -sh "$ZIP_PATH" | cut -f1))"
