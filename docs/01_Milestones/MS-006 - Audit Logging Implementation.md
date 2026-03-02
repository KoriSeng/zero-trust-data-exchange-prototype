---
type: Milestone
milestone_id: MS-006
milestone_name: "Audit Logging Implementation"
milestone_type: "Implementation"
status: "In Progress"
target_date: 2026-03-01
start_date: 2026-02-23
related_decisions: [DR-001]
related_tasks: [TASK-017, TASK-018, TASK-019]
---

## Objective

Implement comprehensive audit logging for the system using CloudWatch Logs to capture all decisions, access attempts, and system events for compliance and security monitoring.

## Deliverables

- CloudWatch Logs configuration and infrastructure
- Audit logging implementation across all components
- Log structure and formatting standards
- Query and analysis documentation
- Retention policies configured

## Infrastructure Decisions

**Lambda runtime:** `dotnet10` managed runtime (not `provided.al2023`). AWS Lambda now supports .NET 10 as a first-class managed runtime; the handler is set to the assembly name (`ZeroTrust.Backend`) and the build produces a framework-dependent zip — no self-contained binary or `bootstrap` rename required.

**CloudWatch Logs delivery:** The CloudWatch Logs VPC Interface Endpoint has been removed to avoid its per-AZ hourly cost. The Lambda function is placed in public VPC subnets (with public IPs assigned to its ENIs), allowing outbound traffic to reach the public CloudWatch Logs endpoint directly via the Internet Gateway — no NAT Gateway required. DocumentDB remains in private subnets with no internet route. The S3 Gateway Endpoint (free) is retained and attached to the public route table for S3 traffic. This is a prototype focused on showcasing application-layer zero trust safety, not network perimeter isolation.

**Network architecture note (for final report):** The chosen topology — Lambda in public subnets, DocumentDB in private subnets — eliminates NAT Gateway runtime cost (~$32–45/month per region), which is significant for an academic prototype with no continuous traffic. The trade-off is that Lambda ENIs are publicly addressable (though no inbound security group rules are open, so this is low-risk here). In a production deployment, the recommended pattern is to keep Lambda in private subnets and provide internet access via a managed NAT Gateway or a NAT instance, combined with VPC Interface Endpoints for AWS services to avoid NAT costs for internal traffic. This network hardening is out of scope for this prototype.
