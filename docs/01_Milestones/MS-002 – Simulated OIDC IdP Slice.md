---
type: Milestone
milestone_id: MS-002
milestone_name: "Simulated OIDC IdP Slice"
milestone_type: "Design"
status: "In Progress"
target_date: 2026-02-07
start_date: 2026-01-25
related_decisions: [DR-001]
related_tasks: [TASK-005, TASK-008, TASK-009, TASK-010, TASK-011]
---

## Objective
Deliver the first vertical slice of the project by designing, implementing, and validating two simulated OIDC identity providers (AWS-hosted) suitable for later Cognito federation.

## Deliverables
- Integration and simulation plan for AWS-hosted OIDC issuers
- Terraform infrastructure baseline for provisioning simulated issuers
- Fixed test user populations and collision scenarios
- Simulated IdP services (Issuer A and Issuer B) with minimal login UI
- k6 smoke tests validating each simulated IdP in isolation

## Success Criteria
- Two distinct issuers are deployed and expose discovery, JWKS, authorize/token endpoints and a minimal login UI.
- k6 smoke tests pass against both issuers and outputs are captured as evidence.
- Secrets are not committed to source control; synthetic credential handling follows CTRL-015.
- The slice is traceable to DR-002 and supporting requirements (REQ-014..REQ-019).
