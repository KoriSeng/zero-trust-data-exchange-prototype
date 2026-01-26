---
type: Requirement
requirement_id: REQ-014
source: "Other"
category: "Security"
owner: "Delivery"
priority: "High"
status: "Proposed"
related_decisions: [DR-001]
related_requirements: [REQ-001, REQ-002]
related_milestones: [MS-002]
related_risks: [RISK-008]
related_controls: [CTRL-012, CTRL-014]
date_identified: 2026-01-24
last_reviewed: 2026-01-24
---

## Requirement Statement

The prototype must demonstrate federation with at least two distinct OIDC identity sources, showing the platform can integrate with multiple external identity providers.

## Rationale

The proposal’s goal is inter-organisational collaboration; demonstrating multiple identity sources strengthens evidence that the approach generalises beyond a single provider.

## Acceptance Interpretation

Two separate OIDC sources are configured for Cognito federation and can each authenticate users into the platform.
