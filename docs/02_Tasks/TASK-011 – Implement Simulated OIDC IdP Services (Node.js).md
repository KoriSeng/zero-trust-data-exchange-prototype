---
type: Task
task_id: TASK-011
title: "Implement Simulated OIDC IdP Services (Node.js)"
owner: STK-001
status: "Not Started"
related_milestone: MS-002
---

## Description

Implement the two simulated OIDC identity providers in Node.js (Express+jose or `oidc-provider`), including a minimal login UI and the fixed test user populations.

## Definition of Done

- Two issuer services exist (Issuer A and Issuer B) with distinct stable issuer URLs.
- Each issuer supports: discovery, JWKS, authorize, token, and a minimal login UI.
- Fixed test user populations are implemented (active/deactivated + collision cases).
- k6 smoke tests run successfully against each issuer in isolation.
- No secrets are committed to source control; synthetic credentials handling follows CTRL-015.
- Traceability: REQ-019 and DR-002 referenced.
