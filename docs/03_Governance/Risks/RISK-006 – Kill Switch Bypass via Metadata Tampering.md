---
type: Risk
risk_id: RISK-006
title: "Kill Switch Bypass via Metadata Tampering"
category: "Technical"
impact: "High"
likelihood: "Low"
risk_level: "Medium"
treatment: "Mitigate"
risk_owner: STK-003
status: "Open"
related_controls: [CTRL-003, CTRL-004]
related_milestones: []
identified_date: 2026-01-24
last_reviewed: 2026-01-24
---

## 1. Risk Description

An actor with write permissions could alter object metadata/tags to remove a revoked/blocked status, bypassing object-level gating and enabling access to a file that should be denied.

## 2. Impact Justification

Bypassing revocation controls enables unauthorized retrieval of sensitive data, undermining the active defense mechanism.

## 3. Mitigation Strategy

Restrict who can modify metadata/tags, segregate duties, and enforce policy conditions so only system roles can change governance tags.

## 4. Residual Risk Assessment

Residual risk depends on correct permission boundaries and prevention of misconfiguration.
