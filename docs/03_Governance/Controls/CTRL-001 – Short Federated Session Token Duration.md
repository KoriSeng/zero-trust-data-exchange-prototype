---
type: Control
control_id: CTRL-001
title: "Short Federated Session / Token Duration"
control_nature: "Technical"
control_type: "Preventive"
control_owner: STK-007
status: "Planned"
related_milestones: []
related_risks: [RISK-001]
mapped_standards: ["NIST SP 800-207"]
last_reviewed: 2026-01-24
---

## 1. Control Description
Configure federated authentication sessions to be short-lived by enforcing strict token/session lifetimes.

## 2. Control Objective
Reduce the window in which a no-longer-authorized collaborator can continue to use an existing session.

## 3. Implementation Details
Set tight OIDC token limits (example in threat model: 1 hour) and ensure session refresh does not extend authorization beyond policy.

## 4. Evidence and Verification
Demonstrate that an access token expires as configured and requires re-authentication after expiry.
