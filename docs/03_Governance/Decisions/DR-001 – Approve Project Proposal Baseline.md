---
type: Decision
decision_id: DR-001
title: "Approve Project Proposal Baseline"
status: "Approved"
decision_area: "Scope"
related_requirements:
  [
    REQ-001,
    REQ-002,
    REQ-003,
    REQ-004,
    REQ-005,
    REQ-006,
    REQ-007,
    REQ-008,
    REQ-009,
    REQ-010,
    REQ-011,
    REQ-012,
    REQ-013,
    REQ-014,
    REQ-015,
    REQ-016,
    REQ-017,
    REQ-018,
    REQ-019,
  ]
related_stakeholders:
  [STK-001, STK-002, STK-003, STK-004, STK-005, STK-006, STK-007]
related_risks:
  [
    RISK-001,
    RISK-002,
    RISK-003,
    RISK-004,
    RISK-005,
    RISK-006,
    RISK-007,
    RISK-008,
    RISK-009,
    RISK-010,
  ]
related_controls:
  [
    CTRL-001,
    CTRL-002,
    CTRL-003,
    CTRL-004,
    CTRL-005,
    CTRL-006,
    CTRL-007,
    CTRL-008,
    CTRL-009,
    CTRL-010,
    CTRL-011,
    CTRL-012,
    CTRL-013,
    CTRL-014,
    CTRL-015,
    CTRL-016,
  ]
supersedes: []
evidence: ["docs/Project Proposal.md"]
---

## Decision Statement

The project will proceed as “Phase 2 (Execution Planning and Delivery)” using the approved proposal as the baseline definition of scope, artefact, and research context.

## Context

The proposal is submitted and approved as the prerequisite gate for initiating the academic project’s execution planning.

## Options Considered

1. Treat the proposal as an informal reference only.
2. Record proposal approval as an explicit governance decision and baseline.

## Decision Outcome

Option B selected to preserve traceability and to avoid implicit authority. The proposal is the source-of-truth baseline for the project charter and subsequent governance artefacts.

## Consequences

- Future scope changes require a new Decision Record that supersedes this baseline.
- The Project Charter must align to this decision and reference DR-001 for authority.
- Initial delivery planning is tracked via milestone MS-001
- OIDC IdP implementation approach is governed by Decision Record DR-002.
