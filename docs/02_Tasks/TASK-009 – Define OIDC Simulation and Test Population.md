---
type: Task
task_id: TASK-009
title: "Define OIDC Simulation and Test Population"
owner: STK-001
status: "Not Started"
related_milestone: MS-002
---

## Description
Define two simulated OIDC identity sources (issuer A and issuer B), their fixed user populations (active/inactive), and collision test cases to demonstrate issuer+subject deconfliction.

## Definition of Done
- Two identity sources defined with distinct issuer identifiers.
- Fixed test user lists defined for each issuer, including active and deactivated users.
- Collision scenarios defined (same display name across issuers; optionally overlapping subject values) and expected behaviour documented.
- Simulation limitations documented and linked to RISK-008.
