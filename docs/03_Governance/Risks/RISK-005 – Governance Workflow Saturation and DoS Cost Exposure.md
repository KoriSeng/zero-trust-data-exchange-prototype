---
type: Risk
risk_id: RISK-005
title: "Governance Workflow Saturation and DoS Cost Exposure"
category: "Delivery"
impact: "Medium"
likelihood: "Medium"
risk_level: "Medium"
treatment: "Mitigate"
risk_owner: STK-007
status: "Open"
related_controls: [CTRL-009]
related_milestones: []
identified_date: 2026-01-24
last_reviewed: 2026-01-24
---

## 1. Risk Description
Automated or malicious request flooding could saturate the approval workflow (e.g., large numbers of access requests), distract data owners/administrators, and potentially create cost spikes in the serverless control plane.

## 2. Impact Justification
Service disruption and cost pressure can reduce availability of the governance process and impede legitimate collaboration.

## 3. Mitigation Strategy
Apply rate limiting, request throttling, and basic abuse controls at the API boundary.

## 4. Residual Risk Assessment
Residual risk remains if attackers can distribute request sources or exploit gaps in throttling rules.
