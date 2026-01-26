---
type: Control
control_id: CTRL-010
title: "Role-Based Route Authorization and Group Mapping"
control_nature: "Technical"
control_type: "Preventive"
control_owner: STK-007
status: "Planned"
related_milestones: []
related_risks: [RISK-004]
mapped_standards: ["NIST SP 800-207"]
last_reviewed: 2026-01-24
---

## 1. Control Description

Restrict administrative functions using role/group-based authorization so users can only perform actions aligned to their role.

## 2. Control Objective

Maintain separation of duties and prevent privilege escalation by ensuring only appropriate roles can perform admin actions.

## 3. Implementation Details

Threat model specifies group mapping (Admin vs DataOwner) and strict route gating via authorizers.

## 4. Evidence and Verification

Demonstrate that a DataOwner role cannot invoke admin onboarding functions and that only Admin can perform those actions.
