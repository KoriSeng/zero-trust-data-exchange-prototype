---
type: Project Charter
project_name: "Zero Trust Data Sharing Platform (Academic)"
initiative_type: "Greenfield"
delivery_model: "Iterative"
initial_state: "New Build"
status: "Draft"
version: v1.0
---

## 1. Purpose
This charter defines the purpose and governance model for Phase 2 of the academic project: execution planning and delivery of a prototype investigation.

The approved baseline for scope and artefact definition is the project proposal (see Decision Record DR-001). The project investigates Zero Trust architectures for secure inter-organisational data exchange in bio-research contexts, focusing on identity assurance, just-in-time access approval, time-bound delivery, revocation, and non-repudiation.

The intended deliverable is a functional, cloud-native prototype on AWS implementing:
- Federated identity gateway (federated SSO)
- Human-in-the-loop governance orchestration for data requests
- Ephemeral delivery (short-lived download mechanisms)
- Surgical revocation controls and auditability

## 2. Authority
This charter does not grant implicit approval. Authority to proceed, and the approved baseline scope for Phase 2, is established by Decision Record DR-001.

Any material change to scope, platform choice, or deliverable definition must be captured in a new Decision Record that supersedes DR-001.

## 3. Governance Model
- Decisions are recorded as Decision Records under `/docs/03_Governance/Decisions`.
- Requirements represent intent only and do not have approval authority without a linked Decision Record.
- Risks, controls, assumptions, constraints, milestones, and tasks are maintained as separate artefacts under `/docs` using their respective templates.
- The project is academic: all organisations and datasets are simulated or synthetic, and the prototype is implemented in an isolated personal environment.
