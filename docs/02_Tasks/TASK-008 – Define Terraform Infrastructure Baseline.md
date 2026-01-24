---
type: Task
task_id: TASK-008
title: "Define Terraform Infrastructure Baseline"
owner: STK-001
status: "Not Started"
related_milestone: MS-002
---

## Description
Define the Terraform approach for provisioning the AWS infrastructure required for the prototype in a repeatable way (environments, state handling approach, and module structure).

## Definition of Done
- Terraform scope defined (which AWS resources are provisioned via IaC), including resources required for simulated OIDC issuers.
- Environment variables/inputs documented (region, naming, tags).
- State handling approach documented (local state vs managed state) appropriate to the academic context.
- Terraform code skeleton exists in the repo and can produce a plan without errors.
- Traceability: requirements REQ-013, REQ-014, REQ-016 and controls CTRL-011, CTRL-014 are considered.
