---
type: Task
task_id: TASK-010
title: "Define k6 Test Plan for Simulated OIDC Providers"
owner: STK-001
status: "Not Started"
related_milestone: MS-002
---

## Description

Define the automated k6 validation approach for the two simulated OIDC providers, including what endpoints and flows are tested and what evidence is captured.

## Definition of Done

- k6 test scenarios defined for each issuer: discovery endpoint, JWKS endpoint, and at least one login/authorization flow.
- Test data (users) referenced from the fixed test population plan.
- Credential injection approach defined for k6 using synthetic credentials only (CTRL-015):
  - Load credentials via environment variables, or
  - Load from a git-ignored local-only config file.
- Evidence capture approach defined (where outputs/logs are stored for later appendices).
- Traceability: requirement REQ-018 and controls CTRL-015 and CTRL-016 are referenced.
