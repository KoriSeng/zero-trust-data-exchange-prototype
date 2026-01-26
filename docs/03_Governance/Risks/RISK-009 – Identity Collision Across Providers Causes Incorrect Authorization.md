---
type: Risk
risk_id: RISK-009
title: "Identity Collision Across Providers Causes Incorrect Authorization"
category: "Security"
impact: "High"
likelihood: "Medium"
risk_level: "High"
treatment: "Mitigate"
risk_owner: STK-003
status: "Open"
related_controls: [CTRL-013]
related_milestones: [MS-002]
identified_date: 2026-01-24
last_reviewed: 2026-01-24
---

## 1. Risk Description

Two different users from different OIDC issuers may share similar identifiers (e.g., display name, email, or overlapping subject identifiers), leading to incorrect de-duplication, authorization decisions, or audit attribution.

## 2. Impact Justification

Identity collision undermines least privilege, can cause data exposure to the wrong person, and breaks non-repudiation.

## 3. Mitigation Strategy

Use issuer + subject as the unique identity key throughout authorization and audit logging, and explicitly test “collision” scenarios.

## 4. Residual Risk Assessment

Residual risk exists if downstream components incorrectly normalize identity attributes or if logs omit issuer context.
