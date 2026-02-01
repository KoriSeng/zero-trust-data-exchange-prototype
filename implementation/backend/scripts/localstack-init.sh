#!/bin/bash
# LocalStack initialization script for Zero Trust Data Exchange
# This script creates S3 buckets and Step Functions state machines on LocalStack startup

set -e

# Wait for LocalStack to be ready
echo "Waiting for LocalStack to be ready..."
while ! awslocal s3 ls 2>/dev/null; do
  sleep 1
done
echo "LocalStack is ready!"

# Configuration
AWS_REGION=${AWS_DEFAULT_REGION:-us-east-1}
DATA_BUCKET="zero-trust-data"
REQUESTS_BUCKET="zero-trust-requests"
STATE_MACHINE_ROLE_ARN="arn:aws:iam::000000000000:role/StepFunctionsRole"

# Create S3 buckets
echo "Creating S3 buckets..."
awslocal s3 mb "s3://${DATA_BUCKET}" --region "$AWS_REGION" 2>/dev/null || echo "Bucket $DATA_BUCKET already exists"
awslocal s3 mb "s3://${REQUESTS_BUCKET}" --region "$AWS_REGION" 2>/dev/null || echo "Bucket $REQUESTS_BUCKET already exists"

# Create IAM role for Step Functions (LocalStack mock)
echo "Creating IAM role for Step Functions..."
awslocal iam create-role \
  --role-name StepFunctionsRole \
  --assume-role-policy-document '{
    "Version": "2012-10-17",
    "Statement": [
      {
        "Effect": "Allow",
        "Principal": {
          "Service": "states.amazonaws.com"
        },
        "Action": "sts:AssumeRole"
      }
    ]
  }' 2>/dev/null || echo "Role StepFunctionsRole already exists"

# Create approval workflow state machine
echo "Creating approval workflow state machine..."
awslocal stepfunctions create-state-machine \
  --name "ZeroTrustApprovalWorkflow" \
  --definition '{
    "Comment": "Data Access Request Approval Workflow",
    "StartAt": "WaitForApproval",
    "States": {
      "WaitForApproval": {
        "Type": "Wait",
        "Seconds": 300,
        "Next": "CheckApprovalStatus"
      },
      "CheckApprovalStatus": {
        "Type": "Pass",
        "Result": "APPROVED",
        "ResultPath": "$.approval_status",
        "Next": "IsApproved"
      },
      "IsApproved": {
        "Type": "Choice",
        "Choices": [
          {
            "Variable": "$.approval_status",
            "StringEquals": "APPROVED",
            "Next": "ProvisionAccess"
          }
        ],
        "Default": "RequestDenied"
      },
      "ProvisionAccess": {
        "Type": "Pass",
        "Result": "Access provisioned successfully",
        "ResultPath": "$.result",
        "Next": "Success"
      },
      "RequestDenied": {
        "Type": "Pass",
        "Result": "Request was denied",
        "ResultPath": "$.result",
        "Next": "Failure"
      },
      "Success": {
        "Type": "Succeed"
      },
      "Failure": {
        "Type": "Fail",
        "Error": "RequestDenied",
        "Cause": "Data access request was not approved"
      }
    }
  }' \
  --role-arn "$STATE_MACHINE_ROLE_ARN" 2>/dev/null || echo "State machine ZeroTrustApprovalWorkflow already exists"

# Create sample data in S3
echo "Creating sample data in S3..."
awslocal s3 cp - "s3://${DATA_BUCKET}/sample-data-001.csv" <<EOF
id,name,email,department
001,Alice Smith,alice@example.com,Engineering
002,Bob Johnson,bob@example.com,Sales
003,Carol Williams,carol@example.com,Marketing
EOF

echo "LocalStack initialization complete!"
echo ""
echo "Created resources:"
echo "  - S3 Bucket: $DATA_BUCKET"
echo "  - S3 Bucket: $REQUESTS_BUCKET"
echo "  - State Machine: ZeroTrustApprovalWorkflow"
echo ""
echo "Access LocalStack at: http://localhost:4566"
