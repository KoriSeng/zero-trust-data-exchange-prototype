---
type: Assumption
assumption_id: ASM-002
status: "Proposed"
source: "Project Proposal.md"
confidence: "High"
scope_area: "Ethics"
related_risks: []
related_decisions: [DR-001]
related_milestones: []
---

## Assumption Statement

The prototype will be implemented and tested only within an isolated personal AWS environment and will not use any employer or third-party production cloud accounts or internal networks.

## Rationale

The proposal requires clear separation from real organisational infrastructure to avoid unintended impact and confidentiality concerns.

## Impact if False

Using real organisational infrastructure could breach policies, create compliance exposure, and undermine the project’s academic integrity.

## Validation Plan

Maintain an environment note documenting the AWS account boundary and ensure all resources are created under that account only.
