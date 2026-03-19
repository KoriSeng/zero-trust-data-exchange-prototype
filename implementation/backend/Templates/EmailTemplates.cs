namespace ZeroTrust.Backend.Templates;

public static class EmailTemplates
{
    // TODO: migrate to Razor templates if this prototype evolves into production-grade notification rendering.

    public static (string Html, string Text) OtpEmail(string requestId, string otpCode)
    {
        var html = $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="UTF-8"><title>Your approval code</title></head>
            <body style="font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f4;padding:40px 0;">
                <tr>
                  <td align="center">
                    <table width="480" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:6px;border:1px solid #e0e0e0;padding:32px 40px;">
                      <tr>
                        <td>
                          <h2 style="color:#1a1a2e;margin-top:0;">Data Access Approval Code</h2>
                          <p style="color:#444;line-height:1.6;">A data access approval has been requested for:</p>
                          <p style="color:#444;"><strong>Request ID:</strong> {requestId}</p>
                          <p style="color:#444;line-height:1.6;">
                            Enter the code below to authorise this approval. This code expires in
                            <strong>10 minutes</strong>.
                          </p>
                          <table cellpadding="0" cellspacing="0" style="margin:24px auto;">
                            <tr>
                              <td style="background:#1a1a2e;border-radius:6px;padding:16px 32px;text-align:center;">
                                <span style="font-size:36px;font-weight:bold;letter-spacing:10px;color:#ffffff;font-family:monospace;">
                                  {otpCode}
                                </span>
                              </td>
                            </tr>
                          </table>
                          <p style="color:#888;font-size:13px;line-height:1.5;">
                            If you did not request this code, ignore this email. Do not share this code with anyone.
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

    public static (string Html, string Text) ApprovalOutcomeEmail(string requestId, string outcome, string? comments)
    {
        var commentsBlock = string.IsNullOrWhiteSpace(comments)
            ? string.Empty
            : $"<p style=\"color:#444;\"><strong>Comments:</strong> {System.Net.WebUtility.HtmlEncode(comments)}</p>";

        var commentsText = string.IsNullOrWhiteSpace(comments)
            ? string.Empty
            : $"\nComments   : {comments}";

        var outcomeColour = outcome.Equals("Approved", StringComparison.OrdinalIgnoreCase)
            ? "#2e7d32"
            : "#c62828";

        var html = $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="UTF-8"><title>Request {outcome}</title></head>
            <body style="font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f4;padding:40px 0;">
                <tr>
                  <td align="center">
                    <table width="480" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:6px;border:1px solid #e0e0e0;padding:32px 40px;">
                      <tr>
                        <td>
                          <h2 style="color:{outcomeColour};margin-top:0;">Request {outcome}</h2>
                          <p style="color:#444;line-height:1.6;">Your data access request has been reviewed.</p>
                          <p style="color:#444;"><strong>Request ID:</strong> {requestId}</p>
                          <p style="color:#444;">
                            <strong>Status:</strong>
                            <span style="color:{outcomeColour};font-weight:bold;">{outcome}</span>
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
