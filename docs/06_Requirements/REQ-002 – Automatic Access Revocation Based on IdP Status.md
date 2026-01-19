---
last_reviewed: 2026-01-24
date_identified: 2026-01-24
related_milestones: []
related_requirements: []
related_decisions: [DR-001]
status: "Proposed"
priority: "High"
owner: "Delivery"
category: "Security"
source: "User Feedback"
requirement_id: REQ-002
type: Requirement
related_risks: [RISK-001]
related_controls: [CTRL-001, CTRL-002]

---

## Requirement Statement

The platform must ensure that when a collaborator is disabled at their parent Identity Provider, they can no longer authenticate to access shared data.

## Rationale
The proposal’s goal is to close the de-provisioning gap by delegating authentication and lifecycle to the partner IdP.

## Acceptance Interpretation
A simulated “disabled user” at the federated IdP cannot successfully authenticate and cannot obtain any new access artefacts.
