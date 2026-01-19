---
type: Assumption
assumption_id: ASM-007
status: "Proposed"
source: "Project Proposal.md"
confidence: "Medium"
scope_area: "Storage"
related_risks: []
related_decisions: [DR-001]
related_milestones: []
---

## Assumption Statement
Object-level revocation can be simulated by updating object metadata/tags and enforcing access via policy so that revoked objects cannot have new access links generated.

## Rationale
The proposal includes an “object-level kill switch” using metadata gating to block pre-signed URL generation for flagged objects.

## Impact if False
If metadata-based gating cannot be implemented effectively, the prototype’s revocation story weakens and may fail to demonstrate the intended Zero Trust control.

## Validation Plan
Create a small test set of objects, toggle revocation flags, and verify that access link generation is blocked consistently while the object remains stored.
