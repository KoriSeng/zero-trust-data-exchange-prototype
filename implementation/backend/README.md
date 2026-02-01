# Zero Trust Data Exchange - Backend

A C# .NET minimal API backend for zero-trust data exchange with approval workflows, federated identity integration, and comprehensive audit logging.

## Architecture

- **Language**: C# .NET 8.0
- **Framework**: ASP.NET Core Minimal APIs
- **Compute**: AWS Lambda with API Gateway (production), local .NET runtime (development)
- **Database**:
  - **Production**: AWS DynamoDB
  - **Local Development**: MongoDB (easier for local testing)
- **Authentication**: Cognito JWT (API Gateway validates signatures)
- **Authorization**: Role-Based Access Control (Requester, DataOwner, Admin, Auditor)

## Key Features

### Just-In-Time (JIT) User Provisioning

- Automatically provisions users from Cognito JWT claims
- Maps Cognito groups to Organizations
- Auto-assigns default Requester role
- First login triggers user creation with organization context

### Federated Identity

- Integrates with Cognito for OAuth 2.0 / OIDC
- Supports multiple IdPs (IDP-A, IDP-B)
- JWT validation at API Gateway (production) or code-level (development)

### Request Workflow

- Submit data access requests with embedded user agreements
- Multi-stage approval process (admin review → owner approval)
- OTP-based redemption for secure access
- Complete audit trail of all actions

### Data Models

- **Users**: Federated identity with organization assignment
- **Organizations**: Entities with Cognito group mapping for JIT
- **Roles**: RBAC with 4 predefined roles
- **UserAgreements**: Versioned terms with immutable audit trail
- **DataAccessRequests**: Self-contained requests with embedded agreements
- **AuditEvents**: Immutable append-only log of all system actions

## Local Development

### Prerequisites

- .NET 8.0 SDK
- MongoDB 5.0+ (or Docker)
- Node.js 18+ (for k6 testing)

### Setup MongoDB

**Option 1: Docker**

```bash
docker run -d -p 27017:27017 --name mongodb mongo:latest
```

**Option 2: Local MongoDB**

```bash
# macOS with Homebrew
brew tap mongodb/brew
brew install mongodb-community@7.0

# Start MongoDB
brew services start mongodb-community@7.0
```

### Build and Run

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run development server
dotnet run

# Server will start at https://localhost:5001
```

### Initialize Development Data

```bash
# Create default roles and sample organizations
curl -X POST https://localhost:5001/dev/initialize
```

## Testing Locally

### Generate Test JWT

```bash
# Create a test JWT payload (no signature validation locally)
# Header: {"alg":"none","typ":"JWT"}
# Payload:
{
  "sub": "cognito-user-id-12345",
  "email": "user@example.com",
  "name": "Test User",
  "cognito:groups": ["ap-southeast-1_XXXXX_IDP-A"]
}

# Encode as: header.payload. (note the trailing dot, no signature)
```

### Example Test Request

```bash
# Get pending requests (requires JWT in Authorization header)
curl -H "Authorization: Bearer eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJzdWIiOiIxMjM0NTY3ODkwIiwiZW1haWwiOiJ1c2VyQGV4YW1wbGUuY29tIiwiY29nbml0bzpncm91cHMiOlsiYXAtc291dGhlYXN0LTFfWFhYWFhfSURQLUEiXX0." \
  https://localhost:5001/requests/pending
```

## API Endpoints

### User & Organization Management

- `POST /organizations` - Create organization (Admin)
- `GET /organizations/{id}` - Get organization details
- `PUT /organizations/{id}` - Update organization (Admin)

### Data Access Requests

- `POST /requests` - Submit new data access request (Requester)
- `GET /requests/{id}` - Get request details
- `GET /requests/pending` - Get pending requests for org (DataOwner/Admin)
- `GET /requests/mine` - Get requester's requests (Requester)
- `POST /requests/{id}/approve` - Approve request (DataOwner/Admin)
- `POST /requests/{id}/deny` - Deny request (DataOwner/Admin)
- `POST /requests/{id}/redeem` - Redeem OTP and get access (Requester)

### User Agreements

- `POST /agreements` - Create new agreement version (Admin)
- `GET /agreements/active` - Get current active agreement (Public)
- `GET /agreements/{id}/versions` - Get agreement version history (Admin)

### Audit Logging

- `GET /audit/events` - Query audit events (Auditor/Admin)
- `GET /audit/events/request/{requestId}` - Get audit events for a request

### Health & Development

- `GET /health` - Health check
- `POST /dev/initialize` - Initialize database with default roles

## Environment Configuration

Edit `appsettings.json` for local settings:

```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://localhost:27017"
  },
  "MongoDb": {
    "DatabaseName": "zero_trust_db"
  }
}
```

For production (Lambda), use environment variables:

- `MONGODB_CONNECTION_STRING` or AWS Secrets Manager
- `DATABASE_NAME` (default: "zero_trust_db")

## Dependency Injection

All services are registered in `Program.cs`:

- `IDataService` - Data access abstraction (MongoDB/DynamoDB)
- `IJwtExtractionService` - JWT claim parsing
- `IJitProvisioningService` - User auto-provisioning from JWT
- Additional services as needed for business logic

## Database Schema

### MongoDB Collections (same schema for DynamoDB tables)

- **users** - User accounts from Cognito
- **roles** - RBAC roles
- **user_roles** - Many-to-many user-role mappings
- **organizations** - Organizations with Cognito group mappings
- **user_agreements** - Versioned terms and conditions
- **requests** - Data access requests with embedded agreements
- **audit_events** - Immutable audit trail

## Production Deployment

### AWS Lambda Setup

1. **Create Lambda function** with .NET 8.0 runtime
2. **Use AWS.Lambda.AspNetCoreServer.Hosting** for HTTP handlers
3. **Replace MongoDbDataService** with DynamoDB implementation
4. **Set environment variables** for DynamoDB region, Cognito pool, etc.
5. **API Gateway** handles JWT signature validation via Lambda authorizer

### DynamoDB Implementation

A `DynamoDbDataService` implementation is planned for production deployment:

- Implements `IDataService` interface
- Uses AWSSDK.DynamoDBv2 for database access
- Swappable with `MongoDataService` via dependency injection

## Security Considerations

- ✅ JWT validated at API Gateway (production)
- ✅ No signature validation required in code (simplifies local testing)
- ✅ User agreements embedded in requests for immutable audit trail
- ✅ All actions logged in audit_events collection
- ✅ JIT provisioning ensures users belong to authorized organizations
- ✅ RBAC enforces action authorization per role

## Development Workflow

1. **Local Testing**: Use MongoDB + test JWTs
2. **Integration Testing**: Use k6 with simulated OIDC providers
3. **Staging**: DynamoDB + Cognito
4. **Production**: Lambda + API Gateway + DynamoDB + Cognito

## Contributing

When adding new features:

1. Update relevant models in `/Models`
2. Add `IDataService` methods for data access
3. Implement methods in `MongoDbDataService`
4. Create/update API endpoints in `Program.cs`
5. Add audit logging for compliance

## References

- [Data Model Documentation](../../docs/Data%20Models%20-%20Backend%20API.md)
- [AWS SDK for .NET](https://github.com/aws/aws-sdk-net)
- [MongoDB.Driver NuGet](https://www.nuget.org/packages/MongoDB.Driver/)
- [ASP.NET Core Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis)
