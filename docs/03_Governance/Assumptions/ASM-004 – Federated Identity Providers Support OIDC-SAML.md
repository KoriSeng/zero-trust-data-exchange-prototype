---
type: Assumption
assumption_id: ASM-004
status: "Proposed"
source: "Project Proposal.md"
confidence: "Medium"
scope_area: "Identity"
related_risks: []
related_decisions: [DR-001]
related_milestones: []
---

## Assumption Statement

External collaborator identities can be represented through federated authentication using standard protocols (OIDC and/or SAML) suitable for integration with the prototype identity gateway.

## Rationale

The artefact design relies on federation to close the de-provisioning gap and avoid static local account provisioning.

## Impact if False

If federation cannot be used, the prototype would revert to local accounts or alternative mechanisms, weakening the research investigation and increasing identity lifecycle risk.

## Validation Plan

Select at least one simulated IdP approach and document the federation flow and claims used for access decisions.
