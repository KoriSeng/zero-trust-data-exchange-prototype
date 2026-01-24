---
type: Requirement
requirement_id: REQ-018
source: "Other"
category: "Technical"
owner: "Delivery"
priority: "Medium"
status: "Proposed"
related_decisions: [DR-001]
related_requirements: [REQ-014, REQ-016, REQ-017]
related_milestones: [MS-002]
related_risks: []
related_controls: [CTRL-016]
date_identified: 2026-01-24
last_reviewed: 2026-01-24
---

## Requirement Statement
The simulated OIDC identity providers must have automated k6 tests that validate core endpoints and the login/authorization flow in isolation before integrating them as Cognito federation sources.

## Rationale
Automated testing improves repeatability and provides evidence that the IdPs behave as expected independently of Cognito configuration.

## Acceptance Interpretation
A k6 test suite can run against Issuer A and Issuer B and confirm (at minimum) discovery, JWKS, and an authorization flow that produces a token response.
