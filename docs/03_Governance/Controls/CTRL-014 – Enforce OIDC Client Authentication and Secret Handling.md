---
type: Control
control_id: CTRL-014
title: "Enforce OIDC Client Authentication and Secret Handling"
control_nature: "Technical"
control_type: "Preventive"
control_owner: STK-007
status: "Planned"
related_milestones: [MS-002]
related_risks: []
mapped_standards: []
last_reviewed: 2026-01-24
---

## 1. Control Description

Configure OIDC clients using appropriate client authentication (client_id and client_secret where applicable) and store secrets securely.

## 2. Control Objective

Prevent accidental insecure federation configuration in the prototype and keep integration patterns aligned with best practices.

## 3. Implementation Details

Use configuration and secret storage mechanisms suitable for the chosen implementation approach; avoid hardcoding secrets in source control.

## 4. Evidence and Verification

Secrets are not committed to the repo and configuration shows client authentication is required and enforced.
