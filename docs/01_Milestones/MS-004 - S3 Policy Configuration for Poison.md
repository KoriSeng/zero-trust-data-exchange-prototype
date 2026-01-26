---
type: Milestone
milestone_id: MS-004
milestone_name: "S3 Policy Configuration for Poison"
milestone_type: "Configuration"
status: "Planned"
target_date: 2026-02-15
start_date: 2026-02-09
related_decisions: [DR-001]
related_tasks: [TASK-007]
---

## Objective

Configure the S3 policy for the poison, ensuring that if there is metadata, downloads are not allowed. This will include a test where a file is placed, metadata is set, and a smoke test is conducted to generate a presigned URL to verify that the file cannot be downloaded.

## Deliverables

- S3 policy configured to restrict downloads based on metadata
- Test file with metadata set
- Smoke test results verifying download restrictions
- Updated findings summary and final investigation/report inputs
