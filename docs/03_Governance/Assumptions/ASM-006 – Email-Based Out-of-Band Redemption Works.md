---
type: Assumption
assumption_id: ASM-006
status: "Proposed"
source: "Project Proposal.md"
confidence: "Medium"
scope_area: "Identity"
related_risks: []
related_decisions: [DR-001]
related_milestones: []
---

## Assumption Statement

A one-time redemption code delivered out-of-band to a verified email inbox is a viable additional step to reduce the risk of session hijacking or federation spoofing within the prototype.

## Rationale

The proposal’s delivery layer includes an out-of-band redemption mechanism to strengthen assurance before generating a time-bound download link.

## Impact if False

If email redemption is unreliable or insecure in the simulated environment, the additional assurance step may not provide meaningful benefit and could harm usability.

## Validation Plan

Implement the redemption flow and run simulated misuse scenarios; document whether redemption materially blocks unauthorized attempts.
