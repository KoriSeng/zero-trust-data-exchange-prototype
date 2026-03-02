---
type: Task
task_id: TASK-018
title: "Implement Audit Logging in Backend Services"
owner: STK-001
status: "Completed"
related_milestone: MS-006
---

## Description

Implement structured audit logging across all backend services including Lambda functions, Step Functions, and API Gateway. Logs should capture all access attempts, approval decisions, data requests, and revocation events with appropriate context and metadata.

## Definition of Done

- [ ] Audit logging implemented in all Lambda functions
- [ ] Step Function state transitions logged
- [ ] API Gateway access logs configured
- [ ] Log structure standardized with required fields
- [ ] Sensitive data properly masked or excluded
- [ ] Integration tests verify logging functionality

## Implementation Notes

- Lambda runtime: `dotnet10` managed runtime; handler `ZeroTrust.Backend` (assembly name via `Amazon.Lambda.AspNetCoreServer.Hosting`)
- CloudWatch Logs delivery: outbound via NAT Gateway (no VPC Interface Endpoint) — reduces running cost for a prototype that focuses on application-layer safety over network perimeter isolation
