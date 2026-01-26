---
type: Risk
risk_id: RISK-002
title: "Pre-Signed URL Leakage Enables Unauthorized Access"
category: "Security"
impact: "Very High"
likelihood: "Medium"
risk_level: "High"
treatment: "Mitigate"
risk_owner: STK-003
status: "Open"
related_controls: [CTRL-007, CTRL-008]
related_milestones: []
identified_date: 2026-01-24
last_reviewed: 2026-01-24
---

## 1. Risk Description

Time-bound download URLs may be copied, forwarded, logged, or otherwise exposed, enabling unintended third parties to access sensitive data during the validity window.

## 2. Impact Justification

Uncontrolled dissemination of high-consequence data can cause irreversible privacy harm and undermine governance intent.

## 3. Mitigation Strategy

Keep URLs short-lived, avoid distributing URLs via email, display them once, and bind access generation to verified identity and explicit approval.

## 4. Residual Risk Assessment

Residual risk remains while any URL is valid; the window is reduced but not eliminated.
