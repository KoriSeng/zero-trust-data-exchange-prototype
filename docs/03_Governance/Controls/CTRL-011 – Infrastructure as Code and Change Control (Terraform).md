---
type: Control
control_id: CTRL-011
title: "Infrastructure as Code and Change Control (Terraform)"
control_nature: "Technical"
control_type: "Preventive"
control_owner: STK-007
status: "Planned"
related_milestones: []
related_risks: [RISK-007]
mapped_standards: []
last_reviewed: 2026-01-24
---

## 1. Control Description
Provision and manage prototype AWS infrastructure using Terraform so security-relevant configuration is version-controlled and repeatable.

## 2. Control Objective
Reduce drift and misconfiguration risk by ensuring infrastructure changes are explicit, reviewable, and reproducible.

## 3. Implementation Details
Maintain Terraform code in the repository and apply changes through a consistent workflow (plan/apply) rather than manual console configuration.

## 4. Evidence and Verification
Demonstrate that a new environment can be created from Terraform and that key settings (e.g., bucket policies, IAM role boundaries, throttling) match the declared configuration.
