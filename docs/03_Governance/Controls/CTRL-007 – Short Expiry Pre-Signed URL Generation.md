---
type: Control
control_id: CTRL-007
title: "Short-Expiry Pre-Signed URL Generation"
control_nature: "Technical"
control_type: "Preventive"
control_owner: STK-007
status: "Planned"
related_milestones: []
related_risks: [RISK-002]
mapped_standards: ["NIST SP 800-207"]
last_reviewed: 2026-01-24
---

## 1. Control Description
Generate pre-signed download URLs with a strict, short expiry time.

## 2. Control Objective
Reduce opportunity for URL leakage misuse by minimizing the time window in which a leaked URL remains valid.

## 3. Implementation Details
Threat model cites setting `x-amz-expires` to a strict limit (example: 15 minutes).

## 4. Evidence and Verification
Show that a URL becomes invalid after its configured expiry and cannot be used again.
