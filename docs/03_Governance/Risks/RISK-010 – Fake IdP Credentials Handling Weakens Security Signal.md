---
type: Risk
risk_id: RISK-010
title: "Fake IdP Credentials Handling Weakens Security Signal"
category: "Security"
impact: "Medium"
likelihood: "Medium"
risk_level: "Medium"
treatment: "Mitigate"
risk_owner: STK-001
status: "Open"
related_controls: [CTRL-015]
related_milestones: [MS-002]
identified_date: 2026-01-24
last_reviewed: 2026-01-24
---

## 1. Risk Description

A simulated IdP login UI may normalize unsafe practices (weak password handling, poor session management, or insecure credential storage), potentially confusing the security narrative of the project.

## 2. Impact Justification

Even in a simulated environment, insecure patterns can undermine the credibility of the investigation and create accidental exposure if reused.

## 3. Mitigation Strategy

Keep credentials synthetic, enforce basic secure handling patterns (no plaintext storage, no secrets in repo), and document simulation boundaries.

## 4. Residual Risk Assessment

Residual risk remains because the IdP is intentionally simplified; limitations must be documented.
