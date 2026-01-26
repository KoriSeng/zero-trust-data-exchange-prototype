---
type: Risk
risk_id: RISK-003
title: "Repudiation of Data Access Actions"
category: "Governance"
impact: "High"
likelihood: "Medium"
risk_level: "High"
treatment: "Mitigate"
risk_owner: STK-003
status: "Open"
related_controls: [CTRL-005, CTRL-006]
related_milestones: []
identified_date: 2026-01-24
last_reviewed: 2026-01-24
---

## 1. Risk Description

A valid user may retrieve data and later deny having requested or accessed it, claiming session hijack or other dispute, reducing accountability.

## 2. Impact Justification

If access cannot be proven end-to-end, the data custodian cannot demonstrate due process or governance, weakening legal defensibility.

## 3. Mitigation Strategy

Establish a chain-of-custody style audit trail across identity, approval workflow, and access events, and add a proof-of-possession step (e.g., one-time redemption code).

## 4. Residual Risk Assessment

Residual risk depends on completeness and integrity of logs and whether correlation identifiers remain consistent.
