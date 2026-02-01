# Backend API Quick Reference

## Current Endpoints (3 Working)

### Health Check

```
GET /health
No authentication required
Response: { "status": "healthy" }
```

### Initialize Database

```
POST /dev/initialize
No authentication required (dev only)
Creates default roles: Requester, DataOwner, Admin, Auditor
Response: { "message": "Database initialized" }
```

### Get Pending Requests

```
GET /requests/pending
Requires: Authorization: Bearer <JWT_TOKEN>
Response: [ { DataAccessRequest objects } ]

Flow:
1. Extracts JWT from Authorization header
2. Auto-provisions user if first login (JIT)
3. Returns requests pending approval for user's organization
```

## Testing with curl

### 1. Generate Test Token

```bash
TOKEN=$(./scripts/generate-jwt.sh "test-sub" "user@example.com" "ap-southeast-1_xxxxx_IDP-A")
echo $TOKEN
```

### 2. Test Endpoints

```bash
# Health check
curl https://localhost:5001/health

# Initialize (optional, /dev/initialize does this too)
curl -X POST https://localhost:5001/dev/initialize

# Get pending requests (requires JWT)
curl -H "Authorization: Bearer $TOKEN" https://localhost:5001/requests/pending
```

## Request/Response Examples

### Successful Request

```
curl -X GET \
  -H "Authorization: Bearer eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJzdWIiOiJ0ZXN0LXVzZXIiLCJlbWFpbCI6InVzZXJAZXhhbXBsZS5jb20iLCJjb2duaXRvOmdyb3VwcyI6WyJhcC1zb3V0aGVhc3QtMV94eHh4eF9JRFAtQSJdfQ." \
  https://localhost:5001/requests/pending

Response 200 OK:
[]
```

### JWT Extraction Success

When JWT is provided, the JwtExtractionService:

1. Reads Authorization header
2. Removes "Bearer " prefix
3. Splits JWT into header.payload.signature
4. Base64 decodes payload
5. Parses JSON claims
6. Returns claims dictionary

### JIT Provisioning Flow

When valid JWT received:

1. Extracts claims: sub, email, name, cognito:groups
2. Looks up Organization by cognito_group_name
3. If organization found:
   - Create new User (if first login)
   - Auto-assign Requester role
   - Return user object
4. If organization not found:
   - Return null (request fails)

## Data Models in Responses

### DataAccessRequest (42 fields)

```json
{
  "_id": "req-uuid",
  "request_id": "REQ-2024-001",
  "requester_id": "user-uuid",
  "requester_email": "alice@org-a.com",
  "requester_org": "ORG-A",
  "data_owner_organization_id": "ORG-B",
  "request_status": "Submitted",
  "agreement_content": "Full text of agreement from agreement table",
  "request_details": "Reason for data request",
  "justification": "Business justification",
  "data_categories": ["PII", "Financial"],
  "created_at": "2024-01-15T10:30:00Z",
  "updated_at": "2024-01-15T10:30:00Z",
  "expires_at": "2024-02-15T10:30:00Z"
}
```

### User (JIT Provisioned)

```json
{
  "_id": "user-uuid",
  "sub": "cognito-subject-id",
  "email": "user@org.com",
  "display_name": "John Doe",
  "organization_id": "ORG-A",
  "status": "Active",
  "created_at": "2024-01-15T10:30:00Z",
  "last_login_at": "2024-01-15T10:30:00Z"
}
```

### Organization (With Cognito Mapping)

```json
{
  "_id": "ORG-A",
  "name": "Organization A",
  "cognito_idp_name": "IDP-A",
  "cognito_group_name": "ap-southeast-1_xxxxx_IDP-A",
  "nda_verified": true,
  "status": "Active"
}
```

## MongoDB Collections

### users

```javascript
db.users.find();
// Indexes: sub (unique), organization_id
```

### organizations

```javascript
db.organizations.find();
// Indexes: cognito_group_name (unique, critical for JIT)
```

### requests

```javascript
db.requests.find({ request_status: "Submitted" });
// Indexes: requester_id, status, data_owner_organization_id + status
```

### user_agreements

```javascript
db.user_agreements.find({ agreement_id: "AGREEMENT-001", version: 1 });
// Indexes: agreement_id + version (unique)
```

### roles, user_roles, audit_events

```javascript
db.roles.find();
db.user_roles.find({ user_id: "user-uuid" });
db.audit_events.find({ request_id: "req-uuid" });
```

## Environment Variables

### Local Development

```bash
# In appsettings.json
ConnectionStrings__MongoDB=mongodb://localhost:27017
MongoDb__DatabaseName=zero_trust_db
```

### Docker MongoDB

```bash
# Default from docker-compose.yml
MONGO_INITDB_DATABASE=zero_trust_db
```

## Common Workflows

### Create Test Organization & User

```bash
# 1. Start MongoDB
docker-compose up -d

# 2. Load test data
./scripts/seed-test-data.sh

# 3. Verify in MongoDB Express
# Visit http://localhost:8081
# Database: zero_trust_db
# Collections: organizations, users, etc.
```

### Test API with JWT

```bash
# 1. Generate JWT for test user
TOKEN=$(./scripts/generate-jwt.sh \
  "test-user-1@cognito" \
  "alice@org-a.com" \
  "ap-southeast-1_xxxxx_IDP-A")

# 2. Call protected endpoint
curl -H "Authorization: Bearer $TOKEN" \
  https://localhost:5001/requests/pending

# 3. Check MongoDB for created user
mongosh mongodb://localhost:27017/zero_trust_db
> db.users.find()
```

## Planned Endpoints (Not Yet Implemented)

### Request Management

```
POST   /requests                      # Submit request
GET    /requests/{id}                 # Get request
GET    /requests/mine                 # My requests
POST   /requests/{id}/approve         # Approve (DataOwner)
POST   /requests/{id}/deny            # Deny request
POST   /requests/{id}/redeem          # Redeem OTP
```

### Organization Management

```
POST   /organizations                 # Create org (Admin)
GET    /organizations/{id}            # Get org
PUT    /organizations/{id}            # Update org
```

### Agreement Management

```
POST   /agreements                    # Create agreement (Admin)
GET    /agreements/active             # Get active
GET    /agreements/{id}/versions      # Version history
```

### Audit & Monitoring

```
GET    /audit/events                  # Query events
GET    /audit/events/request/{id}     # Events for request
```

## Error Handling

### Current Error Responses

**401 Unauthorized**

```json
{ "error": "Authorization header missing" }
```

**400 Bad Request**

```json
{ "error": "Could not provision user" }
```

**500 Internal Server Error**

```
Logged to console
```

## Performance Notes

- **JWT Parsing**: O(1) - single header decode
- **User Lookup**: O(1) - indexed by cognito_group_name
- **Request Queries**: O(log n) - indexed queries
- **Database**: MongoDB 5.0+ with indexes on hot paths

## Security Considerations

### Current Implementation

- ✅ JWT not validated in code (API Gateway handles production)
- ✅ Organization lookup prevents cross-org access
- ✅ JIT provisioning auto-creates users
- ✅ All operations logged to audit_events

### Missing (Planned)

- OTP validation
- S3 pre-signed URLs
- Rate limiting
- CORS configuration
- Data encryption at rest

## Troubleshooting

### MongoDB Connection Failed

```bash
docker ps | grep mongodb
docker-compose logs mongodb
```

### JWT Extraction Errors

- Make sure Authorization header is "Bearer <token>"
- Token must have header.payload.signature format
- Payload must be valid base64 (padding is auto-corrected)

### Port Conflicts

```bash
# Port 5001 already in use?
lsof -i :5001
kill -9 <PID>
```

## References

- Data Models: [Data Models - Backend API.md](../../docs/Data%20Models%20-%20Backend%20API.md)
- Development: [DEVELOPMENT.md](./DEVELOPMENT.md)
- Implementation Status: [IMPLEMENTATION_SUMMARY.md](./IMPLEMENTATION_SUMMARY.md)
