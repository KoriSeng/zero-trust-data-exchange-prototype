---
type: Milestone
milestone_id: MS-002
milestone_name: "Design Baseline"
milestone_type: "Design"
status: "Planned"
target_date: 2026-02-07
start_date: 2026-01-25
related_decisions: [DR-001]
related_tasks: [TASK-002, TASK-003, TASK-004, TASK-005]
---

## Objective
Define the pre-design inputs (data, integrations, evaluation approach) and produce the solution design baseline aligned to the approved proposal.

## Deliverables
- Data and evidence plan (synthetic dataset shapes, tagging schema, required log fields)
- Integration plan (federated IdP simulation, out-of-band redemption approach, Macie/admin trigger simulation)
- Evaluation plan (scenario matrix mapping REQ → RISK → CTRL → evidence)
- Initial solution design document

## Success Criteria
- Pre-design plans exist and are consistent with REQ/RISK/CTRL artefacts.
- The solution design is sufficient to begin implementation without re-litigating scope.
- Any new requirements discovered during planning are captured as REQ-XXX artefacts and linked to risks/controls.
