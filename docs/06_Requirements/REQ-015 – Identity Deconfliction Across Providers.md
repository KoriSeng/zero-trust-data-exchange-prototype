---
type: Requirement
requirement_id: REQ-015
source: "Other"
category: "Security"
owner: "Delivery"
priority: "High"
status: "Proposed"
related_decisions: [DR-001]
related_requirements: [REQ-001, REQ-002, REQ-009]
related_milestones: [MS-002]
related_risks: [RISK-009]
related_controls: [CTRL-013]
date_identified: 2026-01-24
last_reviewed: 2026-01-24
---

## Requirement Statement

The platform must treat identity as a combination of issuer (identity source) and subject so that users from different OIDC providers cannot collide or be confused based on similar names or overlapping identifiers.

## Rationale

In inter-organisational federation, similar usernames/emails can exist across institutions. Deconfliction is required to preserve correct authorization and auditability.

## Acceptance Interpretation

Test cases include users with the same display name across different issuers, and the audit trail and authorization decisions show them as distinct identities.
