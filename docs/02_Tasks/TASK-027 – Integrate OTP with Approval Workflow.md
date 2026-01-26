---
type: Task
task_id: TASK-027
title: "Integrate OTP with Approval Workflow"
owner: STK-001
status: "Not Started"
related_milestone: MS-008
---

## Description

Integrate OTP verification into the approval workflow Step Function and Lambda functions. This includes triggering OTP email on approval requests, validating OTP before processing approvals, and handling failed verification attempts.

## Definition of Done

- [ ] Step Function updated to include OTP verification step
- [ ] Lambda function for OTP email sending implemented
- [ ] Lambda function for OTP validation integrated with approval logic
- [ ] Failed verification handling and retry logic
- [ ] Workflow tested end-to-end with OTP verification
- [ ] Error states properly handled in Step Function
