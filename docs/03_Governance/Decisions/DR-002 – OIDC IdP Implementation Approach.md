---
type: Decision
decision_id: DR-002
title: "OIDC IdP Implementation Approach"
status: "Approved"
decision_area: "Technical"
related_requirements: [REQ-014, REQ-016, REQ-017, REQ-018]
related_stakeholders: [STK-001]
related_risks: [RISK-008, RISK-010]
related_controls: [CTRL-012, CTRL-014, CTRL-015, CTRL-016]
supersedes: []
evidence: ["docs/04_References/OIDC Simulation Plan.md"]
---

## Decision Statement
Implement the two simulated OIDC identity providers using Node.js with either (A) Express + jose as a minimal custom implementation, or (B) the `oidc-provider` library, prioritizing speed of delivery and controlled behaviour over full enterprise fidelity.

## Context
The project requires two simulated OIDC providers with a minimal login UI and predictable test populations to demonstrate Cognito integration with multiple identity sources.

The IdP implementation is not the main research focus. Over-engineering the IdP (or running a full third-party IdP stack) increases delivery risk and setup overhead.

## Options Considered
1. Implement a minimal OIDC provider using Node.js + Express + jose.
2. Use `oidc-provider` (Node.js) to accelerate standards-compliant endpoint behaviour.
3. Use an external hosted IdP service.

## Decision Outcome
Option 1 or 2 approved as acceptable implementation approaches, with selection based on fastest path to a working, testable OIDC flow within the academic timeline.

Both approaches must:
- Support discovery, JWKS, authorize, token endpoints, and a minimal username/password login UI.
- Use synthetic test credentials and avoid committing secrets to source control.
- Provide stable issuer URLs suitable for Terraform-based wiring into Cognito.

## Consequences
- The IdP implementation scope is intentionally minimal and must be documented as a limitation (RISK-008).
- If a materially different IdP approach is later required (e.g., full external IdP stack), a new Decision Record must supersede DR-002.
