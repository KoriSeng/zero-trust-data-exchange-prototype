---
type: Task
task_id: TASK-026
title: "Create Email Templates for OTP and Notifications"
owner: STK-001
status: "In Progress"
related_milestone: MS-008
---

## Description

Write the HTML and plain-text email templates used by `SesService` to send OTP codes and approval outcome notifications. For this prototype, templates are inline C# string constants in a static `EmailTemplates` class — no templating engine, no external files, no images. This keeps the build simple and avoids issues with embedded resource paths in Lambda.

## Implementation Notes

### Design constraints for SES sandbox

- **No external CSS or linked stylesheets** — use only inline `style=""` attributes.
- **No images or base64 embeds** — SES sandbox strips attachments.
- **Plain table layout only** — avoids rendering inconsistencies across email clients.
- **Both HTML and plain-text variants required** — some clients (and spam filters) reject HTML-only emails.

### 1. Create `implementation/backend/Templates/EmailTemplates.cs`

```csharp
namespace ZeroTrust.Backend.Templates;

/// <summary>
/// Inline email templates for OTP delivery and approval outcome notifications.
/// Returns (html, plainText) tuples consumed by SesService.
/// </summary>
public static class EmailTemplates
{
    // -------------------------------------------------------------------------
    // OTP delivery
    // -------------------------------------------------------------------------

    public static (string Html, string Text) OtpEmail(string requestId, string otpCode)
    {
        var html = $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="UTF-8"><title>Your approval code</title></head>
            <body style="font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0;">
              <table width="100%" cellpadding="0" cellspacing="0"
                     style="background:#f4f4f4;padding:40px 0;">
                <tr>
                  <td align="center">
                    <table width="480" cellpadding="0" cellspacing="0"
                           style="background:#ffffff;border-radius:6px;
                                  border:1px solid #e0e0e0;padding:32px 40px;">
                      <tr>
                        <td>
                          <h2 style="color:#1a1a2e;margin-top:0;">
                            Data Access Approval Code
                          </h2>
                          <p style="color:#444;line-height:1.6;">
                            A data access approval has been requested for:
                          </p>
                          <p style="color:#444;">
                            <strong>Request ID:</strong> {requestId}
                          </p>
                          <p style="color:#444;line-height:1.6;">
                            Enter the code below to authorise this approval.
                            This code expires in <strong>10 minutes</strong>.
                          </p>
                          <table cellpadding="0" cellspacing="0" style="margin:24px auto;">
                            <tr>
                              <td style="background:#1a1a2e;border-radius:6px;
                                         padding:16px 32px;text-align:center;">
                                <span style="font-size:36px;font-weight:bold;
                                             letter-spacing:10px;color:#ffffff;
                                             font-family:monospace;">
                                  {otpCode}
                                </span>
                              </td>
                            </tr>
                          </table>
                          <p style="color:#888;font-size:13px;line-height:1.5;">
                            If you did not request this code, ignore this email.
                            Do not share this code with anyone.
                          </p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;

        var text = $"""
            DATA ACCESS APPROVAL CODE
            =========================

            Request ID : {requestId}
            Your code  : {otpCode}

            This code expires in 10 minutes.

            Enter this code in the approval interface to authorise the request.
            If you did not request this code, ignore this email.
            Do not share this code with anyone.
            """;

        return (html, text);
    }

    // -------------------------------------------------------------------------
    // Approval outcome notification
    // -------------------------------------------------------------------------

    public static (string Html, string Text) ApprovalOutcomeEmail(
        string requestId, string outcome, string? comments)
    {
        var commentsBlock = string.IsNullOrWhiteSpace(comments)
            ? string.Empty
            : $"<p style=\"color:#444;\"><strong>Comments:</strong> {comments}</p>";

        var commentsText = string.IsNullOrWhiteSpace(comments)
            ? string.Empty
            : $"\nComments   : {comments}";

        var outcomeColour = outcome.Equals("Approved", StringComparison.OrdinalIgnoreCase)
            ? "#2e7d32"   // green
            : "#c62828";  // red

        var html = $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="UTF-8"><title>Request {outcome}</title></head>
            <body style="font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0;">
              <table width="100%" cellpadding="0" cellspacing="0"
                     style="background:#f4f4f4;padding:40px 0;">
                <tr>
                  <td align="center">
                    <table width="480" cellpadding="0" cellspacing="0"
                           style="background:#ffffff;border-radius:6px;
                                  border:1px solid #e0e0e0;padding:32px 40px;">
                      <tr>
                        <td>
                          <h2 style="color:{outcomeColour};margin-top:0;">
                            Request {outcome}
                          </h2>
                          <p style="color:#444;line-height:1.6;">
                            Your data access request has been reviewed.
                          </p>
                          <p style="color:#444;">
                            <strong>Request ID:</strong> {requestId}
                          </p>
                          <p style="color:#444;">
                            <strong>Status:</strong>
                            <span style="color:{outcomeColour};font-weight:bold;">
                              {outcome}
                            </span>
                          </p>
                          {commentsBlock}
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;

        var text = $"""
            REQUEST {outcome.ToUpperInvariant()}
            {"".PadRight(outcome.Length + 8, '=')}

            Request ID : {requestId}
            Status     : {outcome}{commentsText}
            """;

        return (html, text);
    }
}
```

### 2. Directory placement

```
implementation/backend/
  Templates/
    EmailTemplates.cs      ← new file
  Services/
    SesService.cs          ← consumes EmailTemplates
```

No `.csproj` changes needed — the `Templates/` folder is auto-included because the project uses `<Compile Include="**/*.cs" />` by default.

### 3. Testing templates locally

Because `AWS:SES:Enabled=false` in local dev, `SesService` logs the OTP code to the console. To visually inspect the HTML output without sending a real email, add a temporary integration test or Razor preview page. Alternatively, copy the rendered HTML string into a `.html` file and open it in a browser.

For SES sandbox testing, send to verified addresses and check the inbox. The plain-text variant should render correctly in Gmail, Outlook, and Apple Mail.

### 4. Keeping templates maintainable

For a production system, consider Razor templates, Scriban, or AWS SES Template resources. For this POC, inline strings are sufficient and avoid build complexity. Leave a `// TODO: migrate to Razor templates` comment in the file.

## Definition of Done

- [ ] `Templates/EmailTemplates.cs` created with `OtpEmail` and `ApprovalOutcomeEmail` static methods
- [ ] Both methods return `(string Html, string Text)` tuples
- [ ] HTML uses only inline styles, no images, no external resources
- [ ] OTP template shows: Request ID, 6-digit code, 10-minute expiry warning
- [ ] Outcome template shows: Request ID, Approved/Rejected status (with colour), optional comments field
- [ ] OTP template tested end-to-end via SES sandbox — email received and code visible in inbox
- [ ] Plain-text variant tested (forward email to a plain-text email client or view source)
