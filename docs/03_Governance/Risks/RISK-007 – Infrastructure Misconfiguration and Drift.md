---
type: Risk
risk_id: RISK-007
title: "Infrastructure Misconfiguration and Drift"
category: "Technical"
impact: "High"
likelihood: "Medium"
risk_level: "High"
treatment: "Mitigate"
risk_owner: STK-007
status: "Open"
related_controls: [CTRL-011]
related_milestones: []
identified_date: 2026-01-24
last_reviewed: 2026-01-24
---

## 1. Risk Description

Manual or ad-hoc provisioning of AWS resources may lead to inconsistent environments, configuration drift, and misconfigured security-relevant settings (e.g., IAM policies, bucket policies, logging, throttling).

## 2. Impact Justification

Misconfiguration can undermine confidentiality/integrity controls, invalidate evaluation evidence, and increase time/cost to reproduce or debug issues.

## 3. Mitigation Strategy

Use infrastructure-as-code to define and manage resources consistently, with peer-reviewable changes and repeatable deployments.

## 4. Residual Risk Assessment

Residual risk remains if Terraform code is incomplete or if manual changes occur outside the IaC workflow.
