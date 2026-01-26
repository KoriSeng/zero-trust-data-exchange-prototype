---
type: Risk
risk_id: RISK-008
title: "OIDC Simulation Not Representative of Real Providers"
category: "Technical"
impact: "Medium"
likelihood: "Medium"
risk_level: "Medium"
treatment: "Mitigate"
risk_owner: STK-001
status: "Open"
related_controls: [CTRL-012]
related_milestones: [MS-002]
identified_date: 2026-01-24
last_reviewed: 2026-01-24
---

## 1. Risk Description

A simulated OIDC identity source may behave differently from real-world enterprise IdPs (claim formats, token lifetimes, logout behaviour, key rotation, error modes), reducing the external validity of findings.

## 2. Impact Justification

If the simulation omits key behaviours, the federation results may not generalise, weakening the research evidence.

## 3. Mitigation Strategy

Define the simulation scope explicitly, align to OIDC expectations (discovery document, JWKS, issuer/subject handling), and include at least two simulated providers with different user populations and status changes.

## 4. Residual Risk Assessment

Residual risk remains because the simulation is intentionally constrained; this should be documented as a limitation.
