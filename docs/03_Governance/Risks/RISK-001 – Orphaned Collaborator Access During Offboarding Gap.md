---
type: Risk
risk_id: RISK-001
title: "Orphaned Collaborator Access During Offboarding Gap"
category: "Security"
impact: "Very High"
likelihood: "Medium"
risk_level: "High"
treatment: "Mitigate"
risk_owner: STK-003
status: "Open"
related_controls: [CTRL-001, CTRL-002]
related_milestones: []
identified_date: 2026-01-24
last_reviewed: 2026-01-24
---

## 1. Risk Description

A collaborator who has left (or is no longer authorized by) their home organisation may still retain functional access long enough to retrieve sensitive research data due to delays in identity lifecycle updates and cached sessions.

## 2. Impact Justification

Unauthorized access to high-consequence bio-research data can lead to irreversible privacy harm (re-identification/linkage attacks) and reputational damage.

## 3. Mitigation Strategy

Use federated authentication and enforce short session/token durations so access fails promptly when an account is disabled at the external IdP.

## 4. Residual Risk Assessment

Even with federation, there may be a remaining window where valid sessions exist; residual risk depends on session TTL configuration and enforcement.
