# Investigation into Zero Trust Architectures for Secure Inter-Organizational Data Exchange in Bio-Research Environments

## Overview

This repository contains an **individual academic computing project** undertaken as part of a **Cyber Security degree programme**.

The project focuses on the design and implementation of a **secure cloud-based data sharing platform** that demonstrates the application of cybersecurity principles such as federated identity, least-privilege access, temporary authorisation, and auditability.

This is an **academic project** only. All users, organisations, and data flows are **conceptual or simulated**. No real production data or external organisations are involved.

## Disclaimer (Educational Use Only)

This repository contains **prototype / proof-of-concept code** and **simulated identity-provider behaviour** intended for academic demonstration.

- **Not production-ready:** no security audit or hardening is implied.
- **Not a real Identity Provider:** the OIDC/IdP components are for controlled simulation and testing only.
- **Do not use with real data, real organisations, or real user identities.**
- **No warranty:** the software is provided "as is" under the MIT License (see `LICENSE`).

---

## Repository Structure

The repository is organised to clearly separate **project governance artefacts** from **system implementation**.

```text
/
├── docs/     — Authoritative project documentation and artefacts
├── implementation/      — System source code and implementation
└── README.md — Project overview and navigation
```
---

## docs/

The `docs/` directory contains the **authoritative project artefacts**, maintained as structured, version-controlled Markdown documents. These artefacts collectively demonstrate project planning, governance, and security reasoning.

Typical contents include:

* Project Charter
* Requirements documentation
* Architectural and security design records
* Risk register
* Assumptions and constraints
* Decision records
* Milestone and delivery plans

---

## implementation/

The `implementation/` directory contains the **implementation of the proposed system**, developed to demonstrate the application of cybersecurity principles defined in the project documentation.

The implementation is intended as a **prototype / proof-of-concept**, not a production-ready system.

---

## Methodology

The project follows an **Agile development approach**, with planning and governance artefacts aligned to **PMP principles**.
The Project Charter acts as the authorising document, with detailed artefacts maintained separately to ensure traceability and clarity.

---

## Ethics and Data Handling

* No real personal or production data is used
* All identities and organisations are simulated
* Security and compliance considerations are addressed from an academic perspective
* Audit and access logging are implemented for demonstration purposes only

---

## Usage Notes

This repository is intended for **academic assessment and learning**.
It is not designed or maintained for operational or commercial use.

## Tooling Prerequisites (Local Development)

- Node.js
- AWS CLI
- OpenTofu (`tofu`)
- .NET SDK 10
- `zip` utility (available in your bash/WSL environment)
- Linux-compatible shell environment (Linux/macOS or WSL Ubuntu on Windows)

---

## Simulated IdP Test Users (Development Only)

The prototype includes hardcoded users for the simulated OIDC providers used in local development and demos.

### IDP-A Users (`implementation/idp-a/src/users.js`)

| Username | Password | Display Name | Status |
|---|---|---|---|
| `a.alex` | `pass-a-alex` | Alex Kim | active |
| `a.sam` | `pass-a-sam` | Sam Lee | active |
| `a.pat` | `pass-a-pat` | Pat Chen | deactivated |

### IDP-B Users (`implementation/idp-b/src/users.js`)

| Username | Password | Display Name | Status |
|---|---|---|---|
| `b.alex` | `pass-b-alex` | Alex Kim | active |
| `b.jamie` | `pass-b-jamie` | Jamie Tan | active |
| `b.riley` | `pass-b-riley` | Riley Ng | deactivated |

> These are simulated credentials for this academic prototype only; never reuse in real systems.

---

## Acknowledgements

This project is completed under academic supervision as part of a Cyber Security degree programme.
