---
type: Control
control_id: CTRL-009
title: "Request Rate Limiting and Throttling"
control_nature: "Technical"
control_type: "Preventive"
control_owner: STK-007
status: "Planned"
related_milestones: []
related_risks: [RISK-005]
mapped_standards: ["NIST SP 800-207"]
last_reviewed: 2026-01-24
---

## 1. Control Description

Apply rate limiting and throttling at the API boundary to reduce abusive request volumes.

## 2. Control Objective

Prevent governance workflow saturation and reduce cost/availability impact from automated request flooding.

## 3. Implementation Details

Threat model mentions WAF rate limiting on the request endpoint and API usage plan limits per IP/user.

## 4. Evidence and Verification

Simulate high request volumes and demonstrate throttling behaviour and controlled step-function execution rates.
