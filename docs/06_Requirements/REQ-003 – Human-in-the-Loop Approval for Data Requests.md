---
type: Requirement
requirement_id: REQ-003
source: "User Feedback"
category: "Operational"
owner: "Delivery"
priority: "High"
status: "Proposed"
related_decisions: [DR-001]
related_requirements: []
related_milestones: []
date_identified: 2026-01-24
last_reviewed: 2026-01-24
related_risks: [RISK-004]
related_controls: [CTRL-005, CTRL-010]
---

## Requirement Statement
The platform must require an explicit human approval step by a data owner before any data access artefacts are generated for a request.

## Rationale
The proposal describes a governance orchestration with a human-in-the-loop process to ensure access is intentional and legally defensible.

## Acceptance Interpretation
A request cannot result in any download capability until an approval action is completed by a designated approver role.
