# Data Access Request Submission with LocalStack

## Overview

The request submission feature enables users to request access to data stored in S3. The workflow integrates:

- **MongoDB** for request persistence and audit
- **LocalStack S3** for data storage simulation
- **LocalStack Step Functions** for approval workflow orchestration
- **AWS SDK** for S3 and Step Functions integration

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Frontend / Client                        │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
              POST /requests (Endpoint)
                         │
          ┌──────────────┼──────────────┐
          ▼              ▼              ▼
      ┌────────┐   ┌──────────┐  ┌──────────────┐
      │  JWT   │   │ MongoDB  │  │ LocalStack   │
      │Validate│   │ Storage  │  │ (S3 + SFn)   │
      └────────┘   └──────────┘  └──────────────┘
```

## LocalStack Setup

### Docker Compose Services

The `docker-compose.yml` includes:

```yaml
localstack:
  image: localstack/localstack:latest
  environment:
    SERVICES: s3,stepfunctions,iam,logs
  ports:
    - "4566:4566" # Main LocalStack endpoint
```

### Initialization Script

`scripts/localstack-init.sh` automatically creates:

1. **S3 Buckets**
   - `zero-trust-data`: Contains actual data files
   - `zero-trust-requests`: Stores request metadata and audit trails

2. **Step Functions State Machine**
   - Name: `ZeroTrustApprovalWorkflow`
   - Flow: WaitForApproval → CheckStatus → ProvisionAccess/RequestDenied

3. **IAM Role**
   - Role: `StepFunctionsRole`
   - Allows Step Functions to execute

### Starting LocalStack

```bash
cd implementation/backend
docker-compose up -d

# Verify initialization
docker logs zero-trust-localstack
```

### Accessing LocalStack

```bash
# Via AWS CLI with LocalStack endpoint
awslocal s3 ls s3://zero-trust-data/

# Or configure AWS SDK in application:
AWS:
  ServiceUrl: http://localhost:4566
```

## API Endpoint: POST /requests

### Request

```http
POST /requests
Authorization: Bearer <JWT_TOKEN>
Content-Type: application/json

{
  "datasetId": "dataset-001",
  "datasetName": "Customer Records",
  "objectKeys": [
    "data/customers-2024-q1.csv",
    "data/customers-2024-q2.csv"
  ],
  "purpose": "Quarterly business analytics and reporting",
  "dataOwnerOrg": "ORG-B",
  "metadata": {
    "businessJustification": "Revenue analysis",
    "department": "Finance"
  }
}
```

### Response (201 Created)

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "requestId": "REQ-A1B2C3D4",
  "status": "PendingOwnerApproval",
  "workflowExecutionArn": "arn:aws:states:us-east-1:000000000000:execution:ZeroTrustApprovalWorkflow:REQ-A1B2C3D4-execution-1706806596000",
  "createdAt": "2026-02-01T13:43:16.123Z",
  "message": "Request submitted successfully. Awaiting approval."
}
```

## Workflow Flow

### 1. Request Submission (POST /requests)

```
User submits request
    ↓
Validate JWT and provision user (JIT)
    ↓
Validate request payload (purpose, objects)
    ↓
Check if S3 objects exist
    ↓
Create DataAccessRequest in MongoDB
    ↓
Start Step Functions workflow
    ↓
Upload request metadata to S3 audit bucket
    ↓
Return request ID and workflow ARN
```

### 2. Request Validation

- **JWT Extraction**: Bearer token from Authorization header
- **User Provisioning**: Auto-provision if first login
- **Field Validation**:
  - `purpose` is required and non-empty
  - At least one object key required
  - `dataOwnerOrg` identifies the data custodian
- **S3 Verification**: All requested objects must exist

### 3. Database Storage

Request stored in MongoDB with fields:

```javascript
{
  _id: ObjectId,
  request_id: "REQ-ABC12345",          // Human-readable ID
  requester_id: "user-uuid",            // From JWT
  requester_email: "user@example.com",
  requester_org: "ORG-A",               // User's organization
  dataset_id: "dataset-001",
  dataset_name: "Customer Records",
  object_keys: ["data/file1.csv"],
  purpose: "Business analytics",
  status: "PendingOwnerApproval",       // RequestStatus enum
  data_owner_org: "ORG-B",              // Who owns the data
  created_at: ISODate("2026-02-01T13:43:16Z"),
  updated_at: ISODate("2026-02-01T13:43:16Z"),
  expires_at: ISODate("2026-03-02T13:43:16Z"),
  workflow_execution_arn: "arn:aws:states:...",
  agreement_content: "{...}"           // Embedded legal terms
}
```

### 4. Step Functions Workflow

State machine flow:

```
WaitForApproval (Wait 5 min)
    ↓
CheckApprovalStatus (Pass through - returns APPROVED)
    ↓
IsApproved (Choice)
    ├─→ APPROVED → ProvisionAccess → Success
    └─→ DENIED   → RequestDenied → Failure
```

### 5. Audit Trail in S3

Request metadata stored in S3:

```
s3://zero-trust-requests/
  REQ-ABC12345/
    metadata.json          # Request details
```

Content:

```json
{
  "request_id": "REQ-ABC12345",
  "requester_id": "user-uuid",
  "requester_email": "user@example.com",
  "requester_org": "ORG-A",
  "dataset_id": "dataset-001",
  "purpose": "Business analytics",
  "data_owner_org": "ORG-B",
  "object_keys": ["data/file1.csv"],
  "created_at": "2026-02-01T13:43:16Z",
  "workflow_arn": "arn:aws:states:..."
}
```

## Status Transitions

```
Submitted
    ↓
PendingOwnerApproval (waiting for data owner)
    ├─→ Approved
    │   ├─→ OtpSent (OTP email sent)
    │   └─→ Redeemed (OTP redeemed)
    │       └─→ Completed (access provided)
    │
    └─→ Denied
        └─→ (Request closed)

Expired (automatic after 30 days)
Revoked (manual revocation)
```

## Configuration

### appsettings.json

```json
{
  "AWS": {
    "Region": "us-east-1",
    "ServiceUrl": "http://localhost:4566", // LocalStack
    "S3": {
      "DataBucket": "zero-trust-data",
      "RequestsBucket": "zero-trust-requests"
    },
    "StepFunctions": {
      "ApprovalWorkflowArn": "arn:aws:states:us-east-1:000000000000:stateMachine:ZeroTrustApprovalWorkflow",
      "Enabled": true
    }
  }
}
```

### Services Registered

```csharp
// S3 Service
builder.Services.AddScoped<IS3Service, S3Service>();

// Step Functions Service
builder.Services.AddScoped<IStepFunctionsService, StepFunctionsService>();
```

## Error Handling

### 401 Unauthorized

- Missing or invalid JWT

### 400 Bad Request

- Purpose field is empty
- No object keys provided
- Objects don't exist in S3
- User provisioning failed

### 500 Internal Server Error

- Database save failure
- Workflow start failure (non-fatal - continues without workflow)

## Testing with curl

### 1. Generate Test JWT

```bash
TOKEN=$(./scripts/generate-jwt.sh "test-user-123" "user@example.com" "ap-southeast-1_xxxxx_IDP-A")
```

### 2. Submit Request

```bash
curl -X POST \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "datasetId": "dataset-001",
    "datasetName": "Sample Data",
    "objectKeys": ["sample-data-001.csv"],
    "purpose": "Testing",
    "dataOwnerOrg": "ORG-A"
  }' \
  http://localhost:5000/requests
```

### 3. Check LocalStack

```bash
# View created objects
docker exec zero-trust-localstack awslocal s3 ls s3://zero-trust-requests/ --recursive

# Check workflow status
docker exec zero-trust-localstack awslocal stepfunctions list-executions \
  --state-machine-arn arn:aws:states:us-east-1:000000000000:stateMachine:ZeroTrustApprovalWorkflow
```

## Related Endpoints

- `GET /me` - Get current user info
- `GET /requests/pending` - Get pending requests for approval
- `POST /requests/{id}/approve` - Approve a request (future)
- `POST /requests/{id}/deny` - Deny a request (future)

## Implementation Files

- **Models**: `Models/DataAccessRequest.cs`, `Models/DataAccessRequestDto.cs`
- **Services**: `Services/AwsServices.cs`, `Services/IAwsServices.cs`
- **Data Access**: `Data/MongoDataService.cs`
- **Endpoint**: `Program.cs` (POST /requests)
- **Configuration**: `appsettings.json`, `docker-compose.yml`
- **Scripts**: `scripts/localstack-init.sh`

## Next Steps

1. Implement approval workflow endpoint (PUT /requests/{id}/approve)
2. Add OTP email notification
3. Implement pre-signed URL generation
4. Add request expiration handling
5. Implement request revocation
