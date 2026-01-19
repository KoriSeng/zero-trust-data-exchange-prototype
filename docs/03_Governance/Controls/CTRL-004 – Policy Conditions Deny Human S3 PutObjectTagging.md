---
type: Control
control_id: CTRL-004
title: "Policy Conditions Deny Human S3 PutObjectTagging"
control_nature: "Technical"
control_type: "Preventive"
control_owner: STK-007
status: "Planned"
related_milestones: []
related_risks: [RISK-006]
mapped_standards: ["NIST SP 800-207"]
last_reviewed: 2026-01-24
---

## 1. Control Description
Block human users from modifying S3 object tags used for governance state by denying `s3:PutObjectTagging` except for approved system roles.

## 2. Control Objective
Ensure the kill switch cannot be bypassed by changing object tags from blocked/revoked to active.

## 3. Implementation Details
Use policy condition keys and boundary policies (threat model mentions SCPs/boundary policies) to deny tag mutation for human roles.

## 4. Evidence and Verification
Show that only the orchestration role can alter governance tags and that access requests for revoked objects are blocked.
