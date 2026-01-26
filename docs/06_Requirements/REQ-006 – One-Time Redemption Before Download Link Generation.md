---
type: Requirement
requirement_id: REQ-006
source: "User Feedback"
category: "Security"
owner: "Delivery"
priority: "Medium"
status: "Proposed"
related_decisions: [DR-001]
related_requirements: []
related_milestones: []
date_identified: 2026-01-24
last_reviewed: 2026-01-24
related_risks: [RISK-002, RISK-003]
related_controls: [CTRL-005, CTRL-006]
---

## Requirement Statement

The platform must require the user to redeem a one-time code (delivered out-of-band to a verified email address) before generating the time-bound download link.

## Rationale

The proposal includes out-of-band redemption to add assurance and reduce the impact of session hijacking or federation spoofing.

## Acceptance Interpretation

An approved request does not provide a usable download link until a one-time code is successfully redeemed, and redemption cannot be reused.
