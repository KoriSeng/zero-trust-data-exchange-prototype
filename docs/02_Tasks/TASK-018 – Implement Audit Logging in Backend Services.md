---
type: Task
task_id: TASK-018
title: "Implement Audit Logging in Backend Services"
owner: STK-001
status: "Completed"
related_milestone: MS-006
---

## Description

Structured audit logging is implemented across the backend Lambda function and API Gateway. The Lambda function (ASP.NET Core minimal API, `dotnet10` managed runtime) uses `ILogger<T>` throughout all route handlers and services for operational logging, and calls `dataService.CreateAuditEventAsync(AuditEvent)` at key business-event boundaries to write durable audit records into DocumentDB. API Gateway emits structured JSON access logs for every inbound request. CloudWatch Logs is the centralised log destination for both channels.

Key business events that produce audit records:
- `REQUEST_SUBMITTED` — when a requester submits a new data-access request
- Request **approval** — when an approver accepts a pending request
- Request **rejection** — when an approver denies a pending request
- **Data access redemption** — when an approved requester downloads the dataset

Step Function state-transition logging is **deferred** (see Implementation Notes).

## Definition of Done

- [x] **Audit logging implemented in all Lambda route handlers**
  - `ILogger<T>` injected and used in all service classes and minimal-API route handlers
  - `builder.Services.AddLogging(config => { config.AddConsole(); config.AddDebug(); })` configured in `Program.cs`; `Console` output is automatically captured by the Lambda runtime and forwarded to CloudWatch Logs
- [x] **Durable audit events written to DocumentDB**
  - `IDataService.CreateAuditEventAsync(AuditEvent)` called at: `REQUEST_SUBMITTED`, approval, rejection, and data-access redemption
  - `AuditEvent` fields: `EventType`, `ActorId`, `OrganizationId`, `RequestId`, `DatasetId`, `Description`, `Result`
- [x] **API Gateway access logs configured**
  - Log group: `/aws/apigateway/${project_name}-${environment}-backend` (Terraform-managed, 30-day retention)
  - JSON format per request: `requestId`, `sourceIp`, `requestTime`, `httpMethod`, `routeKey`, `status`, `protocol`, `responseLength`, `integrationErrorMessage`
- [x] **Lambda log group configured**
  - Log group: `/aws/lambda/${project_name}-${environment}-backend` (Terraform-managed, 30-day retention)
  - IAM: `AWSLambdaVPCAccessExecutionRole` grants `logs:CreateLogGroup`, `logs:CreateLogStream`, `logs:PutLogEvents`
- [x] **Log structure standardised**
  - Operational logs: .NET structured logging via `ILogger<T>` (level, message, scoped properties)
  - Audit records: typed `AuditEvent` documents persisted in DocumentDB
- [ ] **Step Function state transitions logged** *(deferred — see Implementation Notes)*
- [ ] **Sensitive data masked**
  - Requester email is present in some `ILogger` log lines — no explicit masking applied
  - ⚠️ Known limitation for this POC (see Implementation Notes)
- [ ] **Integration tests verify logging functionality**
  - Audit-event creation is exercised indirectly by existing integration tests, but no test explicitly asserts CloudWatch log output

## Implementation Notes

### Runtime and log delivery

- **Lambda runtime**: `dotnet10` managed runtime; handler = `ZeroTrust.Backend` (assembly name, resolved by `Amazon.Lambda.AspNetCoreServer.Hosting`)
- **CloudWatch Logs delivery**: outbound HTTPS **through the Internet Gateway (IGW)** — *not* via NAT Gateway and *not* via a VPC Interface Endpoint. The Lambda functions run in public VPC subnets with a route to the IGW. This is intentional: NAT Gateway carries a per-AZ hourly charge and VPC Interface Endpoints add per-hour + per-GB costs that are unnecessary for an academic prototype focused on application-layer zero-trust, not network perimeter isolation.

### Deferred: Step Function state-transition logging

The Step Functions state machine is **not yet managed by Terraform** — it is supplied as an input variable. Because execution-level logging must be configured on the `aws_sfn_state_machine` resource (via the `logging_configuration` block pointing to a dedicated CloudWatch log group), this cannot be added until the SFN resource is brought under Terraform control. This gap is tracked separately and does not block MS-006 acceptance for the prototype.

### Known limitation: requester email in logs

Some `ILogger` call sites log the requester's email address as part of contextual information (e.g., "Processing request for user@example.com"). No masking or redaction is applied. For a production system this would violate data-minimisation principles; for this POC it is accepted as a known limitation. A future task should replace email with an opaque `ActorId` at every log call site.
