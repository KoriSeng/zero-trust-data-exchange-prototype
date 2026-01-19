---
type: Control
control_id: CTRL-003
title: "IAM Role Segregation for Tagging and Orchestration"
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
Separate human administrative roles from system orchestration roles to prevent unauthorized metadata/tag changes.

## 2. Control Objective
Prevent bypass of object-level revocation controls via direct manipulation of S3 object metadata/tags.

## 3. Implementation Details
Define distinct roles (threat model examples: `Human_Admin` and `System_Orchestrator`) and ensure only system roles can perform governance tag changes.

## 4. Evidence and Verification
Attempt to change governance tags as a simulated human role and confirm access is denied while system role operations succeed.
