---
type: Task
task_id: TASK-027
title: "Integrate OTP with Approval Workflow"
owner: STK-001
status: "In Progress"
related_milestone: MS-008
---

## Description

Wire the OTP services (TASK-025) into the existing approval flow by adding two new API endpoints and updating the existing approve endpoint. The Step Function ASL is **not** modified — OTP is enforced at the API layer, keeping the SFN simple. The Step Function continues to start when a request is submitted; the data owner triggers and validates an OTP through the API before approving.

## Approval flow sequence (post-implementation)

```
Requester                  API / Lambda               Data Owner
    │                           │                          │
    │── POST /requests ─────────►│                          │
    │                           │── StartApprovalWorkflow ─►│
    │                           │   (SFN — unchanged)       │
    │                           │                          │
    │                           │◄── (data owner views UI) ─│
    │                           │                          │
    │                           │◄─ POST /requests/{id}/otp/send ──│
    │                           │── GenerateAndSendOtpAsync ──────►│
    │                           │   (SES email → data owner inbox) │
    │                           │                                  │
    │                           │◄─ POST /requests/{id}/approve ───│
    │                           │   { approvalCode, comments }     │
    │                           │── ValidateOtpAsync ──────────────│
    │                           │   (reject if invalid / expired)  │
    │                           │── ProcessApproval ───────────────│
    │◄── notification email ────│                                  │
```

## Implementation Notes

### 1. New endpoint: `POST /requests/{requestId}/otp/send`

This endpoint is called by the data owner's UI when they are ready to approve. It looks up the data owner's email from the existing `DataAccessRequest` / `Organization` record and calls `OtpService.GenerateAndSendOtpAsync`.

Add to `Program.cs` in the requests route group (alongside the existing `/approve` and `/deny` routes):

```csharp
app.MapPost("/requests/{requestId}/otp/send", async (
    string requestId,
    IDataService dataService,
    IOtpService otpService,
    HttpContext httpContext) =>
{
    var currentUser = httpContext.GetCurrentUser(); // existing helper
    if (currentUser is null) return Results.Unauthorized();

    var request = await dataService.GetDataAccessRequestAsync(requestId);
    if (request is null) return Results.NotFound(new { message = "Request not found" });

    // Only the data owner's organization may trigger OTP for their own requests
    if (request.OwnerOrganizationId != currentUser.OrganizationId)
        return Results.Forbid();

    // Retrieve the data owner's email from their organization record
    var ownerOrg = await dataService.GetOrganizationAsync(request.OwnerOrganizationId);
    if (ownerOrg?.ContactEmail is null)
        return Results.Problem("Data owner email not configured", statusCode: 500);

    await otpService.GenerateAndSendOtpAsync(requestId, ownerOrg.ContactEmail);

    return Results.Ok(new { message = "OTP sent to data owner email" });
})
.RequireAuthorization();
```

> **Note on email lookup**: `Organization.ContactEmail` is the field to use. If it is not populated in seed data, add it to `DatabaseSeederHostedService` as part of this task.

### 2. Update `POST /requests/{requestId}/approve`

The existing approve endpoint currently accepts `{ comments: "..." }`. Update the request body to also accept `approvalCode`:

```csharp
// New request record (add alongside existing ApproveRequest DTO or inline):
record ApproveRequestBody(string ApprovalCode, string? Comments);
```

Update the approve handler:

```csharp
app.MapPost("/requests/{requestId}/approve", async (
    string requestId,
    ApproveRequestBody body,
    IDataService dataService,
    IOtpService otpService,
    ISesService sesService,
    HttpContext httpContext) =>
{
    var currentUser = httpContext.GetCurrentUser();
    if (currentUser is null) return Results.Unauthorized();

    var request = await dataService.GetDataAccessRequestAsync(requestId);
    if (request is null) return Results.NotFound(new { message = "Request not found" });

    if (request.OwnerOrganizationId != currentUser.OrganizationId)
        return Results.Forbid();

    // ── OTP gate ──────────────────────────────────────────────────────────────
    if (string.IsNullOrWhiteSpace(body.ApprovalCode))
        return Results.BadRequest(new { message = "approvalCode is required" });

    var otpResult = await otpService.ValidateOtpAsync(requestId, body.ApprovalCode);

    if (otpResult != OtpValidationResult.Success)
    {
        var (statusCode, message) = otpResult switch
        {
            OtpValidationResult.Expired             => (400, "OTP has expired. Request a new code."),
            OtpValidationResult.MaxAttemptsExceeded => (429, "Too many failed attempts. Request a new code."),
            OtpValidationResult.AlreadyUsed         => (400, "OTP has already been used."),
            OtpValidationResult.NotFound            => (400, "No OTP found. Request a code first."),
            _                                       => (400, "Invalid OTP code.")
        };
        return Results.Json(new { message }, statusCode: statusCode);
    }
    // ── End OTP gate ──────────────────────────────────────────────────────────

    // Existing approval logic (unchanged)
    request.Status = RequestStatus.Approved;
    request.ReviewedAt = DateTime.UtcNow;
    request.ReviewComments = body.Comments;
    await dataService.UpdateDataAccessRequestAsync(request);

    await dataService.CreateAuditEventAsync(new AuditEvent
    {
        EventType   = "REQUEST_APPROVED",
        ActorId     = currentUser.Id,
        RequestId   = requestId,
        Description = "Request approved by data owner after OTP validation",
        Result      = AuditEventResult.Success
    });

    // Notify requester of outcome
    var requesterOrg = await dataService.GetOrganizationAsync(request.RequesterOrganizationId);
    if (requesterOrg?.ContactEmail is not null)
    {
        await sesService.SendApprovalOutcomeEmailAsync(
            requesterOrg.ContactEmail, requestId, approved: true, body.Comments);
    }

    return Results.Ok(new { message = "Request approved", requestId });
})
.RequireAuthorization();
```

### 3. Step Functions integration strategy

The Step Function ASL is **not changed**. It starts on request submission (as today) and handles timeout/escalation logic. OTP validation is enforced at the API layer. This avoids:

- Adding a new Lambda-backed SFN task state for OTP (complex ASL changes)
- Managing SFN wait-for-callback tokens across a separate email loop

The approval endpoint is the natural enforcement point: if `ValidateOtpAsync` fails, the approval is blocked regardless of SFN state.

### 4. `appsettings.json` — no new keys needed

The `AWS:SES:Enabled` flag from TASK-024 already controls whether emails are actually sent. In local development, the OTP code appears in Docker logs, making end-to-end testing straightforward without real emails.

### 5. Deny endpoint

The `POST /requests/{requestId}/deny` endpoint does **not** require OTP — denials carry no security-sensitive data access grant. This is a deliberate prototype simplification. Add a comment in the code explaining this decision.

### 6. Manual end-to-end test checklist

Run against a live AWS deployment (or local with `AWS:SES:Enabled=false` and log tailing):

1. Submit a data access request as Requester → confirm SFN execution started
2. As Data Owner, call `POST /requests/{id}/otp/send` → confirm email received (or log entry)
3. As Data Owner, call `POST /requests/{id}/approve` with the OTP code → confirm `200 OK`
4. Call `POST /requests/{id}/approve` again with the same OTP → confirm `400 AlreadyUsed`
5. Repeat step 2–3 with a wrong code three times → confirm `429 MaxAttemptsExceeded` on 3rd attempt

## Definition of Done

- [ ] `POST /requests/{requestId}/otp/send` endpoint implemented and returns `200` when OTP sent (or `403` if wrong organization)
- [ ] `POST /requests/{requestId}/approve` updated to require and validate `approvalCode` in request body
- [ ] `OtpValidationResult` mapped to appropriate HTTP status codes (400, 429)
- [ ] Approval success triggers `SendApprovalOutcomeEmailAsync` to requester
- [ ] Step Function ASL unchanged — OTP enforced at API layer only (rationale documented in code comment)
- [ ] `Organization.ContactEmail` populated in seed data
- [ ] Manual end-to-end checklist above completed against live or local deployment
