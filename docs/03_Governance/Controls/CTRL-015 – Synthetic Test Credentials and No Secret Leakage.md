---
type: Control
control_id: CTRL-015
title: "Synthetic Test Credentials and No Secret Leakage"
control_nature: "Administrative"
control_type: "Preventive"
control_owner: STK-001
status: "Planned"
related_milestones: [MS-002]
related_risks: [RISK-010]
mapped_standards: []
last_reviewed: 2026-01-24
---

## 1. Control Description
Use only synthetic test credentials for the simulated IdP login UI and ensure no secrets or passwords are stored in source control.

## 2. Control Objective
Prevent accidental credential leakage and avoid implying insecure credential handling practices.

## 3. Implementation Details
- Credentials are explicitly non-sensitive and synthetic (e.g., test accounts only; no reuse of any real passwords).
- Credentials may be stored in local-only configuration for testing (e.g., git-ignored files or environment variables).
- Use a `.gitignore`-based approach to prevent leakage.
- Managed secret services (e.g., Secrets Manager) are intentionally not required for Phase 2 unless new scope is approved via Decision Record.

## 4. Evidence and Verification
- Repository scan shows no committed secrets.
- Test credentials are documented as synthetic and used only within the isolated environment.
