---
type: Control
control_id: CTRL-013
title: "Use Issuer + Subject as Unique Identity Key"
control_nature: "Technical"
control_type: "Preventive"
control_owner: STK-007
status: "Planned"
related_milestones: [MS-002]
related_risks: [RISK-009]
mapped_standards: ["NIST SP 800-207"]
last_reviewed: 2026-01-24
---

## 1. Control Description

Treat the combination of OIDC issuer (`iss`) and subject (`sub`) as the unique user identity key in authorization and audit logging.

## 2. Control Objective

Prevent identity collision across federated providers and preserve correct authorization and non-repudiation.

## 3. Implementation Details

Propagate issuer context through the application and logs; avoid using display name/email as unique keys.

## 4. Evidence and Verification

Test users with overlapping names across two issuers and show that audit entries and authorization decisions remain distinct.
