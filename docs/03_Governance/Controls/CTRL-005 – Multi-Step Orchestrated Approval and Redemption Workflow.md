---
type: Control
control_id: CTRL-005
title: "Multi-Step Orchestrated Approval and Redemption Workflow"
control_nature: "Technical"
control_type: "Preventive"
control_owner: STK-007
status: "Planned"
related_milestones: []
related_risks: [RISK-003]
mapped_standards: ["NIST SP 800-207"]
last_reviewed: 2026-01-24
---

## 1. Control Description

Enforce an orchestrated workflow that requires user intent, explicit approval, and proof-of-possession before data access is granted.

## 2. Control Objective

Reduce repudiation risk by tightly linking request intent to subsequent access, and prevent direct access without governance steps.

## 3. Implementation Details

Threat model describes a multi-factor handshake:

- User intent via API request
- Out-of-band token to email
- User submits token before download link is generated

## 4. Evidence and Verification

Show that an access link cannot be generated without completing the workflow steps and that each step creates a timestamped event.
