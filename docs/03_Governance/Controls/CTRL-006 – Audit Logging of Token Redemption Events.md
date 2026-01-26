---
type: Control
control_id: CTRL-006
title: "Audit Logging of Token Redemption Events"
control_nature: "Technical"
control_type: "Detective"
control_owner: STK-007
status: "Planned"
related_milestones: []
related_risks: [RISK-003]
mapped_standards: ["NIST SP 800-207"]
last_reviewed: 2026-01-24
---

## 1. Control Description

Record token redemption / matching events in application logs to provide evidence linking a specific requestor to an access action.

## 2. Control Objective

Support non-repudiation by retaining a forensic record of proof-of-possession events.

## 3. Implementation Details

Threat model specifies that logs record a `Token_Match` event in CloudWatch.

## 4. Evidence and Verification

Demonstrate that a single access event has a corresponding token match record with correlating identifiers and timestamps.
