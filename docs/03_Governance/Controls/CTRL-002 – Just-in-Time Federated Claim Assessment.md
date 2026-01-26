---
type: Control
control_id: CTRL-002
title: "Just-in-Time Federated Claim Assessment"
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

Rely on just-in-time federation checks so authentication fails immediately when the external IdP has disabled the user.

## 2. Control Objective

Close the de-provisioning gap by ensuring the platform does not maintain independent long-lived authorization for external collaborators.

## 3. Implementation Details

Use federated authentication such that the federation handshake depends on current IdP status/claims at login.

## 4. Evidence and Verification

Simulate an IdP-disabled account and show that authentication fails and no access artefacts can be generated.
