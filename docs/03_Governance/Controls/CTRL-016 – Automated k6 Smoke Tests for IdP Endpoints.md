---
type: Control
control_id: CTRL-016
title: "Automated k6 Smoke Tests for IdP Endpoints"
control_nature: "Operational"
control_type: "Detective"
control_owner: STK-001
status: "Planned"
related_milestones: [MS-002]
related_risks: []
mapped_standards: []
last_reviewed: 2026-01-24
---

## 1. Control Description
Use k6 to run repeatable smoke tests against each simulated OIDC provider’s endpoints and minimal login/authorization flow.

## 2. Control Objective
Detect regressions in the IdP simulation and provide repeatable evidence of IdP behaviour prior to server-side federation configuration.

## 3. Implementation Details
Implement a small k6 script suite that validates discovery, JWKS, and a controlled authorization flow.

## 4. Evidence and Verification
k6 results show passing checks for each issuer and are stored as evidence (appendix) during validation.
