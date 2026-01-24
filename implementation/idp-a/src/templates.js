export function renderLoginPage({ issuerDisplayName, client_id, redirect_uri, state, scope, response_type, error }) {
  const escaped = (s) => String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/\"/g, '&quot;');

  return `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>${escaped(issuerDisplayName)} Login</title>
  <style>
    body { font-family: system-ui, Arial; padding: 24px; max-width: 680px; margin: auto; }
    .card { border: 1px solid #ddd; border-radius: 10px; padding: 16px; }
    label { display: block; margin-top: 12px; }
    input { width: 100%; padding: 8px; }
    .error { color: #b00020; margin-top: 12px; }
  </style>
</head>
<body>
  <h1>${escaped(issuerDisplayName)} (Simulated)</h1>
  <div class="card">
    <p>This is a simulated IdP login screen for MS-002 testing.</p>
    ${error ? `<div class="error">${escaped(error)}</div>` : ''}

    <form method="post" action="/login">
      <input type="hidden" name="client_id" value="${escaped(client_id)}" />
      <input type="hidden" name="redirect_uri" value="${escaped(redirect_uri)}" />
      <input type="hidden" name="state" value="${escaped(state)}" />
      <input type="hidden" name="scope" value="${escaped(scope)}" />
      <input type="hidden" name="response_type" value="${escaped(response_type)}" />

      <label>Username
        <input name="username" autocomplete="username" />
      </label>
      <label>Password
        <input type="password" name="password" autocomplete="current-password" />
      </label>
      <button type="submit" style="margin-top: 14px;">Sign in</button>
    </form>
  </div>
</body>
</html>`;
}
