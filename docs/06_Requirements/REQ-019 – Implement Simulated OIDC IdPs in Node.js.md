---
type: Requirement
requirement_id: REQ-019
source: "Other"
category: "Technical"
owner: "Delivery"
priority: "Medium"
status: "Proposed"
related_decisions: [DR-001, DR-002]
related_requirements: [REQ-014, REQ-016, REQ-017, REQ-018]
related_milestones: [MS-003]
related_risks: [RISK-008, RISK-010]
related_controls: [CTRL-012, CTRL-014, CTRL-015, CTRL-016]
date_identified: 2026-01-24
last_reviewed: 2026-01-24
---

## Requirement Statement
The two simulated OIDC identity providers must be implemented in Node.js to enable rapid delivery and controlled behaviour, using either a minimal Express+jose implementation or the `oidc-provider` library.

## Rationale
The IdP implementation supports the research artefact but is not the research focus; adopting a lightweight Node.js approach reduces delivery risk while still enabling realistic OIDC integration and automated testing.

## Acceptance Interpretation
Two issuer services exist (Issuer A and Issuer B) implemented in Node.js, expose the required OIDC endpoints and login UI, and can be validated with k6 before being configured as Cognito federation sources.
