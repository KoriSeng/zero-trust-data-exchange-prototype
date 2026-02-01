# Data Models - Backend API for Approval Workflow

**Document Version:** 1.0  
**Status:** Draft  
**Related Milestone:** MS-005  
**Related Tasks:** TASK-012, TASK-013, TASK-014  
**Last Updated:** 2026-02-01

---

## 1. Overview

This document defines the core data models for the approval workflow backend. These models support:

- **User identity and authorization** from Cognito JWT claims
- **Role-based access control** (RBAC) with group mapping
- **Request lifecycle management** for data access workflows
- **Approval tracking** with multi-actor orchestration
- **Audit trail** for non-repudiation

### 1.1 Design Principles

1. **Federated Identity First**: User identity is derived from Cognito JWT (`sub`, `cognito:username`, `cognito:groups`)
2. **Immutable Audit Trail**: All state transitions are logged with timestamps and actor identity
3. **Group-Based Authorization**: Cognito groups map to application roles (`DataOwner`, `Admin`, `Requester`)
4. **Deduplication Across IdPs**: Users with same name from different organizations are distinguished by IdP issuer + sub
5. **No Local Passwords**: Authentication is delegated to federated IdPs via Cognito

---

## 2. User Identity Model

### 2.1 Identity Claims from Cognito JWT

When a user authenticates through Cognito (federated via IDP-A or IDP-B), the JWT contains:

```json
{
  "sub": "ap-southeast-1_lq8PdxNkF_IDP-A:A-001",
  "cognito:username": "IDP-A_a.alex",
  "email": "alex@example.org",
  "name": "Alex Kim",
  "cognito:groups": ["Requester"],
  "identities": [
    {
      "userId": "A-001",
      "providerName": "IDP-A",
      "providerType": "OIDC",
      "issuer": "https://4frzzoipgddhrof4fhexntahxi0thuhl.lambda-url.ap-southeast-1.on.aws/",
      "primary": "true"
    }
  ]
}
```

### 2.2 Internal User Representation

**Entity: `User` (Relational Database)**

The backend maps Cognito claims to an internal user model. Role assignment is stored separately in a `UserRole` junction table:

| Field              | Type         | Source                       | Description                                             |
| ------------------ | ------------ | ---------------------------- | ------------------------------------------------------- |
| `id`               | Integer (PK) | Auto-increment               | Internal user ID                                        |
| `sub`              | String (UK)  | Cognito `sub`                | Cognito subject (unique across pool, stable identifier) |
| `cognitoUsername`  | String       | `cognito:username`           | Cognito-prefixed username (e.g., `IDP-A_a.alex`)        |
| `federatedSub`     | String       | `identities[0].userId`       | Original IdP subject (e.g., `A-001`)                    |
| `identityProvider` | String       | `identities[0].providerName` | IdP name (e.g., `IDP-A`, `IDP-B`)                       |
| `issuer`           | String       | `identities[0].issuer`       | IdP issuer URL                                          |
| `email`            | String       | `email`                      | User's email address                                    |
| `displayName`      | String       | `name`                       | User's display name                                     |
| `organizationId`   | String       | Derived from IdP             | Organization identifier (e.g., `ORG-A`, `ORG-B`)        |
| `createdAt`        | ISO8601      | First access                 | Timestamp of first authentication                       |
| `lastAccessAt`     | ISO8601      | Current access               | Timestamp of last authentication                        |
| `status`           | Enum         | Manual assignment            | `ACTIVE`, `SUSPENDED`, `INACTIVE`                       |

**Entity: `Role` (Relational Database)**

| Field       | Type         | Description                                                    |
| ----------- | ------------ | -------------------------------------------------------------- |
| `id`        | Integer (PK) | Role ID                                                        |
| `name`      | String (UK)  | Role name (e.g., `Requester`, `DataOwner`, `Admin`, `Auditor`) |
| `createdAt` | ISO8601      | Role creation timestamp                                        |

**Entity: `UserRole` (Junction Table - Relational Database)**

| Field           | Type         | Description                  |
| --------------- | ------------ | ---------------------------- |
| `userId`        | Integer (FK) | Reference to `User.id`       |
| `roleId`        | Integer (FK) | Reference to `Role.id`       |
| `assignedAt`    | ISO8601      | Timestamp of role assignment |
| `assignedBy`    | String       | Admin who assigned the role  |
| **Primary Key** | Composite    | `(userId, roleId)`           |

**Key Design Decisions:**

1. **Primary Key**: Use Cognito `sub` as unique identifier (stable across all IdPs in pool)
2. **Internal User ID**: Auto-incrementing `id` for database efficiency
3. **Role Storage**: Roles stored in internal database, independent of Cognito groups
4. **Flexible Role Assignment**: Users from IDP-A or IDP-B can be assigned any role (e.g., DataOwner can come from either IdP)
5. **Deduplication**: Two users named "Alex Kim" from IDP-A and IDP-B have different `sub` values
6. **Organization Mapping**: `organizationId` derived from IdP name (e.g., `IDP-A` → `ORG-A`)

### 2.3 Role and Authorization Mapping

**Role Definitions:**

| Role Name   | Capabilities                                              | Typical Source    |
| ----------- | --------------------------------------------------------- | ----------------- |
| `Requester` | Submit data access requests                               | IDP-A or IDP-B    |
| `DataOwner` | Approve/deny requests, view pending requests              | IDP-A (typically) |
| `Admin`     | Onboard organizations, manage policies, view all requests | IDP-A (internal)  |
| `Auditor`   | Read-only access to audit logs                            | IDP-A (internal)  |

**Role Assignment Strategy:**

- Roles are assigned internally via the `UserRole` junction table
- **NOT** derived from Cognito groups (which are IdP-specific like `ap-southeast-1_lq8PdxNkF_IDP-A`)
- Cognito `sub` is the stable identifier for role lookup
- Roles are independent of IdP assignment (flexible: DataOwner can come from IDP-A or IDP-B)

**Authorization Logic:**

- API endpoints are protected by **Cognito Authorizer** in API Gateway
- Lambda extracts `sub` claim from JWT
- Lambda queries `UserRole` table to fetch user's assigned roles from internal database
- Access decisions use **Attribute-Based Access Control (ABAC)**:
  - `DataOwner` can approve requests only for their organization
  - `Admin` can approve requests across organizations

**Example Authorization Check (C# .NET):**

```csharp
public async Task<bool> CanApproveRequestAsync(string sub, DataAccessRequest request)
{
    // Look up user from DynamoDB
    var user = await _userService.GetUserBySubAsync(sub);
    if (user == null) return false;

    var roles = await _roleService.GetUserRolesAsync(user.Id);
    var roleNames = roles.Select(r => r.Name).ToList();

    // Admin can approve anything
    if (roleNames.Contains("Admin")) return true;

    // DataOwner can approve only their org's requests
    if (roleNames.Contains("DataOwner"))
    {
        return user.OrganizationId == request.DataOwnerOrg;
    }

    return false;
}
```

---

## 3. Data Access Request Model

### 3.1 Request Lifecycle

```
DRAFT → SUBMITTED → PENDING_ADMIN_REVIEW → PENDING_OWNER_APPROVAL
  → APPROVED → OTP_SENT → REDEEMED → COMPLETED

Alternative paths:
  → DENIED (from any PENDING state)
  → EXPIRED (if TTL exceeded)
  → REVOKED (manual admin action)
```

### 3.2 Request Entity

**Entity: `DataAccessRequest` (DynamoDB)**

| Field                   | Type        | Description                                                     |
| ----------------------- | ----------- | --------------------------------------------------------------- |
| `requestId`             | String (PK) | UUID v4 (e.g., `req-550e8400-e29b-41d4-a716-446655440000`)      |
| `requesterId`           | String (FK) | Cognito sub of requester                                        |
| `requesterEmail`        | String      | Email for OTP delivery                                          |
| `requesterOrg`          | String      | Organization of requester (e.g., `ORG-A`)                       |
| `datasetId`             | String      | Identifier for requested dataset (e.g., `dataset-genomic-2024`) |
| `datasetName`           | String      | Human-readable dataset name                                     |
| `objectKeys`            | String[]    | S3 object keys requested (e.g., `["samples/safe.txt"]`)         |
| `purpose`               | String      | Stated purpose/justification for access                         |
| `status`                | Enum        | Current workflow state (see lifecycle above)                    |
| `dataOwnerOrg`          | String      | Organization that owns the dataset (e.g., `ORG-CUSTODIAN`)      |
| `createdAt`             | ISO8601     | Request submission timestamp                                    |
| `updatedAt`             | ISO8601     | Last state change timestamp                                     |
| `expiresAt`             | ISO8601     | Request expiration (e.g., 7 days from creation)                 |
| `approvedBy`            | String      | Cognito sub of approver (null if not approved)                  |
| `approvedAt`            | ISO8601     | Approval -timestamp                                             |
| `deniedBy`              | String      | Cognito sub of denier (null if not denied)                      |
| `deniedAt`              | ISO8601     | Denial timestamp                                                |
| `denialReason`          | String      | Optional reason for denial                                      |
| `otpToken`              | String      | Hashed OTP token (for redemption)                               |
| `otpExpiresAt`          | ISO8601     | OTP expiration (e.g., 15 minutes from approval)                 |
| `redeemedAt`            | ISO8601     | Timestamp when OTP was redeemed                                 |
| `presignedUrl`          | String      | Generated pre-signed S3 URL (ephemeral, logged but not stored)  |
| `presignedUrlExpiresAt` | ISO8601     | Pre-signed URL expiration (e.g., 15 minutes)                    |
| `accessedAt`            | ISO8601     | Timestamp when data was downloaded (from S3 access logs)        |
| `revokedAt`             | ISO8601     | Manual revocation timestamp                                     |
| `revokedBy`             | String      | Cognito sub of revoker                                          |
| `userAgreementId`       | String      | Reference to user agreement tied to this request                |
| `userAgreementVersion`  | Integer     | Version of user agreement at time of request                    |
| `agreementContent`      | JSON        | Complete agreement content (copy at request time for context)   |

**Indexes:**

- Primary: `requestId`
- Secondary: `requesterId` (query by requester)
- Secondary: `status` + `dataOwnerOrg` (query pending approvals for a data owner)
- Secondary: `createdAt` (temporal queries)

---

## 4. Approval Tracking Model

### 4.1 Approval Workflow

The system supports multi-step approval:

1. **Admin Review** (optional): Verify NDA/legal compliance
2. **Data Owner Approval**: Explicit consent from data custodian

**Entity: `ApprovalStep`**

| Field         | Type        | Description                                            |
| ------------- | ----------- | ------------------------------------------------------ |
| `approvalId`  | String (PK) | UUID v4                                                |
| `requestId`   | String (FK) | Associated request                                     |
| `stepType`    | Enum        | `ADMIN_REVIEW`, `OWNER_APPROVAL`                       |
| `assignedTo`  | String      | Cognito sub of assigned approver (null = any eligible) |
| `assignedOrg` | String      | Organization of approver                               |
| `status`      | Enum        | `PENDING`, `APPROVED`, `DENIED`, `SKIPPED`             |
| `actedBy`     | String      | Cognito sub of actor who approved/denied               |
| `actedAt`     | ISO8601     | Action timestamp                                       |
| `comments`    | String      | Optional approver comments                             |
| `createdAt`   | ISO8601     | Step creation timestamp                                |

**Example Multi-Step Flow:**

```json
{
  "requestId": "req-123",
  "approvalSteps": [
    {
      "stepType": "ADMIN_REVIEW",
      "assignedOrg": "ORG-CUSTODIAN",
      "status": "APPROVED",
      "actedBy": "admin-001",
      "actedAt": "2026-02-01T10:30:00Z"
    },
    {
      "stepType": "OWNER_APPROVAL",
      "assignedOrg": "ORG-CUSTODIAN",
      "status": "PENDING",
      "actedBy": null
    }
  ]
}
```

---

## 5. OTP Redemption Model

### 5.1 OTP Token Design

Upon approval, the system generates a one-time password (OTP) and sends it to the requester's email. The requester must submit the OTP to receive the pre-signed S3 URL.

**Security Considerations:**

- OTP stored as **salted hash** (bcrypt) in `DataAccessRequest.otpToken`
- OTP expires in **15 minutes** (`otpExpiresAt`)
- OTP is **single-use** (status transitions to `REDEEMED`)
- Pre-signed URL generated **only after** valid OTP submission

**Entity: `OTPRedemption`** (Audit Trail Only)

| Field                   | Type        | Description                                    |
| ----------------------- | ----------- | ---------------------------------------------- |
| `redemptionId`          | String (PK) | UUID v4                                        |
| `requestId`             | String (FK) | Associated request                             |
| `redeemedBy`            | String      | Cognito sub of redeemer (must match requester) |
| `redeemedAt`            | ISO8601     | Redemption timestamp                           |
| `ipAddress`             | String      | Client IP address                              |
| `userAgent`             | String      | Client user agent                              |
| `otpValid`              | Boolean     | Whether OTP was valid                          |
| `presignedUrlGenerated` | Boolean     | Whether URL was successfully generated         |

---

## 6. Audit Event Model

### 6.1 Immutable Audit Trail

All state transitions and authorization decisions are logged to **DynamoDB** and **CloudWatch Logs**.

**Entity: `AuditEvent`**

| Field        | Type         | Description                                              |
| ------------ | ------------ | -------------------------------------------------------- |
| `eventId`    | String (PK)  | UUID v4                                                  |
| `timestamp`  | ISO8601 (SK) | Event timestamp (sort key for temporal queries)          |
| `eventType`  | Enum         | Event category (see below)                               |
| `actorId`    | String       | Cognito sub of actor                                     |
| `actorOrg`   | String       | Actor's organization                                     |
| `actorRole`  | String[]     | Actor's roles at time of event                           |
| `requestId`  | String       | Associated request (if applicable)                       |
| `resourceId` | String       | Resource identifier (e.g., S3 object key)                |
| `action`     | String       | Action performed (e.g., `REQUEST_SUBMITTED`, `APPROVED`) |
| `outcome`    | Enum         | `SUCCESS`, `FAILURE`, `DENIED`                           |
| `details`    | JSON         | Additional context (e.g., denial reason, OTP attempts)   |
| `ipAddress`  | String       | Client IP                                                |
| `userAgent`  | String       | Client user agent                                        |

**Event Types:**

- `AUTHENTICATION`: User logged in via Cognito
- `AUTHORIZATION`: Permission check performed
- `REQUEST_LIFECYCLE`: Request state change (submit, approve, deny, expire)
- `DATA_ACCESS`: Pre-signed URL generated or S3 object accessed
- `ADMIN_ACTION`: Administrative action (revoke, policy change)

**Example Audit Event:**

```json
{
  "eventId": "evt-789",
  "timestamp": "2026-02-01T10:35:42Z",
  "eventType": "REQUEST_LIFECYCLE",
  "actorId": "dataowner-001",
  "actorOrg": "ORG-CUSTODIAN",
  "actorRole": ["DataOwner"],
  "requestId": "req-123",
  "action": "APPROVED",
  "outcome": "SUCCESS",
  "details": {
    "comments": "Approved for genomic study XYZ",
    "previousStatus": "PENDING_OWNER_APPROVAL",
    "newStatus": "APPROVED"
  }
}
```

---

## 7. Organization and Dataset Models

### 7.1 Organization Entity

**Entity: `Organization`**

| Field              | Type        | Description                                                                |
| ------------------ | ----------- | -------------------------------------------------------------------------- |
| `organizationId`   | String (PK) | Unique org identifier (e.g., `ORG-A`, `ORG-B`)                             |
| `name`             | String      | Organization display name                                                  |
| `cognitoIdpName`   | String      | Cognito identity provider name (e.g., `IDP-A`)                             |
| `cognitoGroupName` | String (UK) | Auto-generated Cognito group name (e.g., `ap-southeast-1_lq8PdxNkF_IDP-B`) |
| `idpIssuer`        | String      | OIDC issuer URL                                                            |
| `status`           | Enum        | `ACTIVE`, `SUSPENDED`, `DEPROVISIONED`                                     |
| `onboardedAt`      | ISO8601     | Timestamp of onboarding                                                    |
| `onboardedBy`      | String      | Admin who onboarded the org                                                |
| `ndaVerified`      | Boolean     | Whether NDA is on file                                                     |
| `ndaExpiresAt`     | ISO8601     | NDA expiration date                                                        |

**Use Case:**

- Admin onboards a new partner organization with its auto-generated Cognito group name
- `cognitoGroupName` enables direct mapping during JIT user provisioning
- When a user authenticates, their JWT `cognito:groups` claim is matched against this field
- Automatically assigns user to organization without additional lookup/parsing
- Tracks compliance status (NDA verification)

**JIT Provisioning Flow:**

1. User logs in via Cognito (IDP-A or IDP-B)
2. JWT contains `cognito:groups` = `ap-southeast-1_lq8PdxNkF_IDP-B` (auto-generated)
3. Lambda extracts `cognito:groups[0]` value
4. Queries `Organization` table by `cognitoGroupName` to find matching org
5. Creates/updates user record with the matched `organizationId`
6. User is immediately assigned to correct organization on first login

**POC Implementation (Current):**

For the POC phase, organizations are **pre-seeded** with:

- `organizationId`, `name`, `cognitoIdpName`
- `cognitoGroupName` (obtained from Cognito User Pool after IdP is manually registered)
- `idpIssuer` (from IdP configuration)

This approach allows testing without AWS Console access.

**Future Enhancement (Product Phase):**

In a full product deployment, an admin-only backend API endpoint (`POST /admin/organizations`) will:

1. Accept organization details (name, IdP configuration)
2. Automatically register the IdP with Cognito User Pool via Cognito API
3. Retrieve the auto-generated Cognito group name from Cognito
4. Store the organization in the database with all Cognito metadata
5. Eliminate need for manual AWS Console configuration

This will enable self-service organization onboarding without direct AWS access.

---

**Entity: `Dataset`**

| Field                 | Type        | Description                                  |
| --------------------- | ----------- | -------------------------------------------- |
| `datasetId`           | String (PK) | Unique dataset identifier                    |
| `name`                | String      | Dataset display name                         |
| `description`         | String      | Dataset description                          |
| `ownerOrg`            | String      | Organization that owns the data              |
| `s3Bucket`            | String      | S3 bucket name                               |
| `s3KeyPrefix`         | String      | S3 key prefix (e.g., `genomics/study-2024/`) |
| `classification`      | Enum        | `PUBLIC`, `SENSITIVE`, `RESTRICTED`          |
| `requiresNda`         | Boolean     | Whether NDA is required for access           |
| `requiresAdminReview` | Boolean     | Whether admin review is required             |
| `createdAt`           | ISO8601     | Dataset creation timestamp                   |
| `updatedAt`           | ISO8601     | Last update timestamp                        |

---

## 8. Storage Implementation

### 8.1 DynamoDB-Only Database Design

**Architecture Decision**: All data is stored in DynamoDB for simplicity, cost efficiency, and to enable self-contained requests with full agreement context.

**DynamoDB Tables:**

**1. Users Table: `zero-trust-users`**

- **Partition Key (PK)**: `sub` (Cognito subject - globally unique across pool)
- **Columns**: `id`, `sub`, `cognitoUsername`, `federatedSub`, `identityProvider`, `issuer`, `email`, `displayName`, `organizationId`, `status`, `createdAt`, `lastAccessAt`
- **GSI-1**: `organizationId` (PK), `createdAt` (SK) - query users by organization
- **Purpose**: Store federated user accounts with JIT provisioning

**2. Roles Table: `zero-trust-roles`**

- **Partition Key (PK)**: `roleId` (UUID)
- **Columns**: `roleId`, `name`, `createdAt`
- **GSI-1**: `name` (PK) - query role by name for lookups
- **Purpose**: Store role definitions (Requester, DataOwner, Admin, Auditor)

**3. User Roles Table: `zero-trust-user-roles`**

- **Partition Key (PK)**: `userId#roleId` (composite)
- **Columns**: `userId`, `roleId`, `assignedAt`, `assignedBy`
- **GSI-1**: `userId` (PK), `assignedAt` (SK) - query roles for a user
- **Purpose**: Map users to roles (many-to-many)

**4. Requests Table: `zero-trust-requests`**

- **Partition Key (PK)**: `requestId`
- **Sort Key (SK)**: `createdAt` (for range queries)
- **Columns**: All fields from Section 3.2, including `agreementContent` (full nested JSON)
- **GSI-1**: `requesterId` (PK), `createdAt` (SK) - query by requester
- **GSI-2**: `status#dataOwnerOrg` (PK), `createdAt` (SK) - query pending approvals
- **TTL**: `expiresAt` - auto-delete expired requests after 90 days
- **Purpose**: Self-contained requests with complete agreement context embedded

**5. Audit Events Table: `zero-trust-audit-events`**

- **Partition Key (PK)**: `eventType#date` (e.g., `REQUEST_LIFECYCLE#2026-02-01`)
- **Sort Key (SK)**: `timestamp`
- **GSI-1**: `requestId` (PK), `timestamp` (SK) - query all events for a request
- **GSI-2**: `actorId` (PK), `timestamp` (SK) - query all actions by a user
- **Purpose**: Immutable audit trail

**6. Organizations Table: `zero-trust-organizations`**

- **Partition Key (PK)**: `organizationId`
- **Columns**: All fields from Section 7.1
- **Purpose**: Organization metadata with Cognito group mapping

**7. Datasets Table: `zero-trust-datasets`**

- **Partition Key (PK)**: `datasetId`
- **GSI-1**: `ownerOrg` (PK), `createdAt` (SK) - query datasets by owner
- **Purpose**: Dataset metadata

**8. User Agreements Table: `zero-trust-user-agreements` (NEW)**

- **Partition Key (PK)**: `agreementId`
- **Sort Key (SK)**: `version` (Integer)
- **Columns**: `agreementId`, `version`, `content`, `effectiveDate`, `expiryDate`, `createdAt`, `status`
- **GSI-1**: `status` (PK), `createdAt` (SK) - query active agreements
- **Purpose**: Store user agreements with version history. Complete content copied to requests for immutable audit trail.

---

## 9. API Contract Considerations

### 9.1 Authentication and Authorization Flow

**Architecture: JWT Validation at API Gateway, Claims Processing in .NET**

**Production Environment:**

**Step 1: Initial Authentication via Cognito**

1. User authenticates via Cognito hosted UI (federated via IDP-A or IDP-B)
2. Cognito issues JWT with `sub`, `cognito:username`, `email`, `cognito:groups`, `identities`
3. Client sends JWT in `Authorization: Bearer <token>` header

**Step 2: API Gateway & Cognito Authorizer (Authentication)**

4. **API Gateway validates JWT via Cognito Authorizer** ← _Authentication responsibility_
   - Verifies JWT signature against Cognito public key
   - Validates token expiration, issuer, audience
   - Rejects invalid/expired tokens with 401 Unauthorized
5. Only valid JWTs are passed through to Lambda

**Step 3: .NET Lambda (Claims Processing)**

6. Lambda receives JWT in request headers (already validated by API Gateway)
7. Lambda **trusts** JWT claims (no signature validation needed)
8. Lambda extracts claims: `sub`, `cognito:groups`, `email`, `cognito:username`, `name`

**Step 4: JIT User Provisioning**

9. Lambda extracts `cognito:groups[0]` (auto-generated group name)
10. Queries `Organization` table by `cognitoGroupName` to get `organizationId`
11. Checks if user with this `sub` exists in `User` table:
    - **If exists**: Update `lastAccessAt` timestamp
    - **If not exists**: Create new user record with the matched `organizationId`
12. JIT provisioning automatically assigns user to correct organization on first login

**Step 5: Role-Based Authorization (Authorization)**

13. Lambda queries `User` table by `sub` to get user ID
14. Lambda queries `UserRole` table to fetch assigned roles
15. Construct user context with roles for authorization decisions
16. Enforce ABAC rules (DataOwner can approve only their org, Admin can approve any org)

---

**Local Testing Environment:**

For local smoke tests and k6 load tests, you can **bypass API Gateway validation**:

1. **Test Client generates custom JWT** with desired claims:
   ```json
   {
     "sub": "test-user-123",
     "email": "test@example.org",
     "name": "Test User",
     "cognito:username": "test-user",
     "cognito:groups": ["ap-southeast-1_lq8PdxNkF_IDP-A"]
   }
   ```
2. **Test Client signs JWT** locally (any key, since no validation in Lambda)
3. **Direct Lambda invocation** or **local API Gateway mock** forwards JWT to Lambda
4. **Lambda extracts claims as-is** (no signature verification)
5. Rest of flow proceeds normally

**Testing Benefits:**

- No dependency on live Cognito service
- Can test with pre-seeded users and organizations
- Can generate JWTs for any organization/role combination
- Full end-to-end flow testable locally with k6 and smoke tests

**Example: C# Code Accepts Any JWT (No Validation)**

```csharp
public async Task<(User, List<Role>, Organization)> AuthenticateAndProvisionUserAsync(
    HttpRequest request,
    IConfiguration config)
{
    // Extract JWT from Authorization header
    var authHeader = request.Headers["Authorization"].ToString();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        throw new UnauthorizedAccessException("Missing or invalid Authorization header");

    var token = authHeader.Substring("Bearer ".Length);

    // In production: API Gateway already validated this
    // In local tests: No validation needed - we trust the test JWT
    var handler = new JwtSecurityTokenHandler();
    JwtSecurityToken jwt;

    try
    {
        // Read JWT WITHOUT validating signature
        jwt = handler.ReadJwtToken(token);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Failed to parse JWT: {ex.Message}");
    }

    // Extract claims (already validated by API Gateway in production)
    var claims = jwt.Claims;
    var sub = claims.FirstOrDefault(c => c.Type == "sub")?.Value;
    var cognitoGroups = claims
        .Where(c => c.Type == "cognito:groups")
        .Select(c => c.Value)
        .ToList();
    var email = claims.FirstOrDefault(c => c.Type == "email")?.Value;
    var displayName = claims.FirstOrDefault(c => c.Type == "name")?.Value;

    if (string.IsNullOrEmpty(sub))
        throw new InvalidOperationException("JWT missing 'sub' claim");

    // Extract auto-generated group name
    var cognitoGroupName = cognitoGroups.FirstOrDefault();
    if (string.IsNullOrEmpty(cognitoGroupName))
        throw new UnauthorizedAccessException("No Cognito groups found in JWT");

    // Look up organization by Cognito group name
    var org = await _dynamoDbService.QueryOrganizationByCognitoGroupAsync(cognitoGroupName);
    if (org == null)
        throw new InvalidOperationException($"Organization not found for group: {cognitoGroupName}");

    // Create or update user (JIT provisioning)
    var user = await _dynamoDbService.GetUserBySubAsync(sub);
    if (user == null)
    {
        user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Sub = sub,
            Email = email,
            DisplayName = displayName,
            OrganizationId = org.OrganizationId,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            LastAccessAt = DateTime.UtcNow
        };
        await _dynamoDbService.PutUserAsync(user);
    }
    else
    {
        user.LastAccessAt = DateTime.UtcNow;
        await _dynamoDbService.UpdateUserAsync(user);
    }

    // Fetch user roles
    var roles = await _dynamoDbService.GetUserRolesAsync(user.Id);

    return (user, roles, org);
}
```

---

### 9.2 Request Submission Flow

1. `POST /requests`
   - Request Body: `{ datasetId, purpose, objectKeys }`
   - Lambda extracts `userId`, `email`, `organizationId` from JWT
   - Creates `DataAccessRequest` in DynamoDB with status `SUBMITTED`
   - Triggers Step Function execution for approval workflow
   - Returns: `{ requestId, status }`

### 9.3 Approval Flow

1. `GET /requests/pending`
   - Query requests where `status = PENDING_OWNER_APPROVAL` and `dataOwnerOrg = user.organizationId`
   - Returns: Array of pending requests

2. `POST /requests/{requestId}/approve`
   - Request Body: `{ comments }`
   - Validates user has `DataOwner` or `Admin` role
   - Updates request status to `APPROVED`
   - Generates OTP and sends email via SES
   - Returns: `{ success, otpSent }`

### 9.4 OTP Redemption Flow

1. `POST /requests/{requestId}/redeem`
   - Request Body: `{ otp }`
   - Validates OTP hash, expiration, and single-use
   - Generates pre-signed S3 URL (15-minute expiration)
   - Updates request status to `REDEEMED`
   - Returns: `{ presignedUrl, expiresAt }`

---

## 10. User Agreements Model

### 10.1 User Agreement Entity

**Entity: `UserAgreement` (DynamoDB)**

| Field            | Type         | Description                                                      |
| ---------------- | ------------ | ---------------------------------------------------------------- |
| `agreementId`    | String (PK)  | UUID v4 (e.g., `agreement-550e8400-e29b-41d4-a716-446655440000`) |
| `version`        | Integer (SK) | Version number (1, 2, 3, etc.) - enables version history         |
| `name`           | String       | Agreement name (e.g., "Data Access Terms")                       |
| `content`        | String (LOB) | Full agreement text/HTML (can be large)                          |
| `effectiveDate`  | ISO8601      | When agreement becomes effective                                 |
| `expiryDate`     | ISO8601      | When agreement expires (null = indefinite)                       |
| `status`         | Enum         | `ACTIVE`, `ARCHIVED`, `DEPRECATED`                               |
| `createdAt`      | ISO8601      | Agreement creation timestamp                                     |
| `createdBy`      | String       | Admin who created the agreement                                  |
| `organizationId` | String       | Organization that created the agreement (null = system-wide)     |

**Design Rationale:**

- **Versioning**: Multiple versions of same agreement can coexist (version 1, 2, 3, etc.)
- **Self-Contained Requests**: When user submits request, the entire agreement content (at that version) is copied into the request as `agreementContent`
- **Immutable Audit Trail**: Each request captures the exact agreement terms the user accepted
- **Compliance**: Future audits can prove what terms applied to each data access request

**Request-Agreement Binding:**

When user submits data access request:

1. Fetch active `UserAgreement` for requester's organization
2. Copy entire `content` field to `request.agreementContent`
3. Record `agreementId` and `agreementVersion` in request
4. Request is now self-contained with full legal context

**Example Request-Agreement Flow (C# .NET):**

```csharp
public async Task<DataAccessRequest> SubmitRequestAsync(SubmitRequestCommand cmd, User requester)
{
    // Fetch active agreement for requester's organization
    var agreement = await _dynamoDbService.GetActiveAgreementAsync(
        organizationId: requester.OrganizationId
    );

    if (agreement == null)
        throw new InvalidOperationException(
            $"No active agreement for organization: {requester.OrganizationId}"
        );

    // Create request with embedded agreement content
    var request = new DataAccessRequest
    {
        RequestId = Guid.NewGuid().ToString(),
        RequesterId = requester.Sub,
        RequesterEmail = requester.Email,
        RequesterOrg = requester.OrganizationId,
        DatasetId = cmd.DatasetId,
        Purpose = cmd.Purpose,
        ObjectKeys = cmd.ObjectKeys,
        Status = RequestStatus.Submitted,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        // Bind agreement to request
        UserAgreementId = agreement.AgreementId,
        UserAgreementVersion = agreement.Version,
        AgreementContent = agreement.Content // FULL CONTENT COPIED HERE
    };

    await _dynamoDbService.PutRequestAsync(request);
    await _auditService.LogRequestSubmittedAsync(request, requester);

    return request;
}
```

---

## 11. Next Steps

1. **Finalize data model** with stakeholder feedback
2. **Define API contract** (TASK-012) using these data models
3. **Implement .NET Lambda handlers** (TASK-013) with DynamoDB operations using AWS SDK
4. **Design Step Function state machine** (TASK-014) for workflow orchestration
5. **Create test data generator** for sample users, organizations, agreements, and requests

---

## 12. Open Questions and Resolved Decisions

**Resolved Decisions:**

1. ✅ **Role Assignment**: Roles are stored in DynamoDB (`zero-trust-user-roles` table), NOT derived from Cognito groups. This allows flexible assignment (DataOwner can come from IDP-A or IDP-B).
2. ✅ **User Deduplication**: Using Cognito `sub` as unique identifier (stable across all IdPs in pool). Two users named "Alex Kim" from different IdPs have different `sub` values.
3. ✅ **Organization Mapping**: Organizations store the auto-generated Cognito group name (`cognitoGroupName`). During JIT provisioning, users are automatically assigned to the correct organization by matching their JWT `cognito:groups` claim against this field.
4. ✅ **Test Case Setup**: IDP-A = internal people (ORG-A), IDP-B = external requestors (ORG-B). DataOwner typically from IDP-A, but can be assigned from either IdP.
5. ✅ **User Provisioning**: Just-in-Time (JIT) provisioning enabled. User records are created on-demand when they first authenticate, with organization auto-assigned based on Cognito group name.
6. ✅ **Database Stack**: All DynamoDB (no relational database). Enables self-contained requests with embedded agreement context, simpler architecture, lower cost.
7. ✅ **Backend Runtime**: C# .NET with minimal APIs on AWS Lambda. Better for productization and type safety. API Gateway + Lambda + .NET architecture.
8. ✅ **User Agreements**: Stored separately with versioning. Complete agreement content copied to each request for immutable audit trail and self-contained context.

**Remaining Questions:**

1. **Organization Auto-Discovery**: Should we auto-create `Organization` records from IdP claims, or require explicit admin onboarding (with Cognito group name populated manually)?
2. **Multi-Approver**: Should we support multiple approvers per request (e.g., 2-of-3 approval)?
3. **Revocation Scope**: When revoking a request, should we invalidate pre-signed URLs via S3 object tagging or bucket policies?
4. **Audit Log Retention**: How long should audit events be retained (currently mentions 90 days for request TTL)?
5. **Default Role Assignment**: When a user is provisioned via JIT, should they automatically receive the `Requester` role, or require explicit admin assignment?
6. **Agreement Acceptance**: Should users explicitly accept agreements before submitting requests, or is implicit acceptance via request submission sufficient?

---

## 13. Future Enhancements

### 13.1 Self-Service Organization Onboarding (Product Phase)

**Current State (POC):**

- Organizations are pre-seeded in database and Cognito User Pool
- Manual IdP registration via AWS Console required
- Manual entry of Cognito group names

**Future Enhancement:**
Admin-only backend API: `POST /admin/organizations`

**Request Payload:**

```json
{
  "organizationName": "Partner Organization XYZ",
  "idpType": "OIDC",
  "idpIssuer": "https://partner-idp.example.com",
  "idpClientId": "client-id",
  "idpClientSecret": "client-secret",
  "ndaVerified": true
}
```

**Backend Workflow:**

1. Validate admin credentials via `Admin` role check
2. Register IdP with Cognito User Pool using AWS Cognito API:
   - Call `AdminCreateUserPoolDomain` or `CreateUserPoolIdentityProvider`
   - Retrieve auto-generated Cognito group name from response
3. Create `Organization` record with returned group name
4. Return organization ID and onboarding status

**Benefits:**

- Eliminates need for AWS Console access by partners
- Enables self-service organization onboarding in product phase
- Maintains secure, auditable record of onboarding actions

**Implementation Timing:**

- POC (current): Manual pre-seeding
- Product Phase (future): API-driven provisioning

### 13.2 Additional Future Considerations

1. **Automated Approval Workflows**: Add time-based auto-approval escalation if no action within SLA
2. **Bulk Data Access Requests**: Support requests for multiple datasets in single workflow
3. **Access Delegation**: Allow DataOwner to delegate approval authority to team members
4. **Fine-Grained Audit Filtering**: Audit log query API with search/filter capabilities
5. **Role-Based Audit Access**: Different audit log visibility based on user role

---

**Document Status:** Ready for MS-005 Implementation with C# .NET Backend.
