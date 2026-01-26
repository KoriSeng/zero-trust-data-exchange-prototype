---
type: Risk
risk_id: RISK-004
title: "Privilege Escalation and Segregation of Duties Failure"
category: "Security"
impact: "High"
likelihood: "Medium"
risk_level: "High"
treatment: "Mitigate"
risk_owner: STK-003
status: "Open"
related_controls: [CTRL-010]
related_milestones: []
identified_date: 2026-01-24
last_reviewed: 2026-01-24
---

## 1. Risk Description

A user with limited responsibilities (e.g., data owner/approver) may gain or be granted administrative capabilities (e.g., onboarding new organisations, bypassing governance controls), violating separation of duties.

## 2. Impact Justification

Over-privileged access increases the likelihood of unauthorized sharing, collusion, or bypass of approval/audit controls.

## 3. Mitigation Strategy

Enforce role-based access boundaries in the application and identity layer and restrict administrative actions to dedicated admin roles.

## 4. Residual Risk Assessment

Residual risk depends on correctness of authorization mappings and prevention of misconfiguration.
