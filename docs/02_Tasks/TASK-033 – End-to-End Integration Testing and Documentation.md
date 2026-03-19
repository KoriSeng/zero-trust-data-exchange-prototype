---
type: Task
task_id: TASK-033
title: "End-to-End Integration Testing and Documentation"
owner: STK-001
status: "In Progress"
related_milestone: MS-009
---

## Description

Validate the complete SPA happy path through a documented manual test script, add a basic Playwright smoke test for CI confidence, write a developer setup README, and produce a short demo script for a live walkthrough with non-technical observers.

## Implementation Notes

### 1. Manual test script — `implementation/spa/TESTING.md`

Create this file and document each step of the full end-to-end flow. The script should be executable by anyone with valid IDP-A and IDP-B test accounts and access to the deployed environment.

**Template:**

```markdown
# Manual Test Script — Zero Trust Data Exchange SPA

## Prerequisites
- Two browser profiles (or incognito windows) to hold separate sessions
- IDP-A test account (Requester role) — credentials from test population doc
- IDP-B test account (DataOwner role) — credentials from test population doc
- SPA running at http://localhost:5173 OR the CloudFront URL
- Access to the IDP-B user's email inbox (for OTP)

---

## TC-01 — Requester Login and Request Submission

1. Open the SPA. Expect: Login page with two sign-in buttons.
2. Click **Sign in as Requester (IDP-A)**.
   Expect: Redirected to Cognito Hosted UI, then to IDP-A login page.
3. Enter IDP-A credentials and complete login.
   Expect: Redirected to `/callback`, then to the Dashboard.
4. Verify the header shows the IDP-A user's display name and organisation.
5. Click **+ New Request**.
6. Fill in the form:
   - Dataset ID: `DS-001`
   - Dataset Name: `Test Dataset`
   - Purpose: `Academic research`
   - Object Keys: `data/sample.csv`
   - Data Owner Org: `ORG-B`
7. Click **Submit Request**.
   Expect: Redirected to My Requests list with a success banner and the new Request ID.
8. Note the Request ID for later steps.
   Expect: Request status is `Submitted` or `PendingOwnerApproval`.

---

## TC-02 — Data Owner Login and Approval

1. Open a second browser profile / incognito window.
2. Navigate to the SPA. Click **Sign in as Data Owner (IDP-B)**.
3. Complete IDP-B login.
   Expect: Dashboard shows the Pending Approvals section.
4. Verify the request from TC-01 appears in the list.
5. Click **Review** on the request.
   Expect: Request details panel expands showing requester, dataset, purpose, object keys.
6. Click **Send verification code to my email**.
   Expect: Success message "✓ Verification code sent to your email."
7. Check the IDP-B user's email inbox. Copy the 6-digit OTP.
8. Enter the OTP in the code field.
9. Optionally enter a comment.
10. Click **Approve Request**.
    Expect: Request disappears from the pending list.

---

## TC-03 — Requester Downloads Approved File

1. Return to the first browser profile (IDP-A session).
2. Navigate to My Requests (auto-refresh or manual refresh).
   Expect: The request status has changed to `Approved`.
3. Click **⬇ Download** on the approved request.
   Expect: A new tab opens with a pre-signed S3 URL and the file downloads (or a browser download prompt appears).

---

## TC-04 — Rejection Flow

1. As IDP-A, submit a second request (same form fields as TC-01).
2. Switch to IDP-B session. Find the new request.
3. Click **Reject** on the request.
4. Enter a reason: `Insufficient justification.`
5. Click **Confirm Rejection**.
   Expect: Request disappears from the pending list.
6. Switch back to IDP-A session. Refresh My Requests.
   Expect: The second request shows status `Rejected`.

---

## TC-05 — OTP Invalid Code

1. Repeat TC-02 steps 1–6 (send OTP).
2. Enter an intentionally wrong code: `000000`.
3. Click **Approve Request**.
   Expect: Error message displayed inline, e.g. "Invalid or expired code. 2 attempts remaining."
4. Enter the correct code and approve successfully.
```

### 2. Playwright smoke tests

Install Playwright in the `implementation/spa/` project:

```bash
npm install -D @playwright/test
npx playwright install chromium firefox
```

Create `implementation/spa/tests/smoke.spec.js`:

```js
import { test, expect } from '@playwright/test';

const BASE = process.env.SPA_BASE_URL ?? 'http://localhost:5173';
const API  = process.env.VITE_API_BASE_URL;

test('health endpoint returns 200', async ({ request }) => {
  const response = await request.get(`${API}/health`);
  expect(response.status()).toBe(200);
});

test('login page renders with both IdP buttons', async ({ page }) => {
  await page.goto(`${BASE}/login`);
  await expect(page.getByText('Sign in as Requester (IDP-A)')).toBeVisible();
  await expect(page.getByText('Sign in as Data Owner (IDP-B)')).toBeVisible();
});

test('unauthenticated access to dashboard redirects to login', async ({ page }) => {
  await page.goto(`${BASE}/`);
  await expect(page).toHaveURL(/\/login/);
});
```

Add a Playwright config (`playwright.config.js`):

```js
import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './tests',
  use: { baseURL: process.env.SPA_BASE_URL ?? 'http://localhost:5173' },
  projects: [
    { name: 'chromium', use: { browserName: 'chromium' } },
    { name: 'firefox',  use: { browserName: 'firefox'  } }
  ]
});
```

Add to `package.json`:

```json
"scripts": {
  "test:e2e": "playwright test"
}
```

> **Note on full E2E with real Cognito:** The Cognito Hosted UI makes true automated login difficult (it's a redirected browser flow). For the POC, mock the auth layer in unit tests and rely on the manual test script for end-to-end validation. The smoke tests above only cover public surfaces (health check, login page render, redirect guard).

### 3. Developer setup guide — `implementation/spa/README.md`

```markdown
# Zero Trust Data Exchange — SPA

React 18 + Vite single-page application for the zero-trust data exchange prototype.

## Prerequisites

| Tool | Version |
|------|---------|
| Node.js | 18 or later |
| npm | 9 or later |
| AWS CLI | v2 (for deployment only) |

## Local development setup

### 1. Install dependencies

cd implementation/spa
npm install

### 2. Configure environment

cp .env.example .env

Fill in the values from Terraform outputs:

| Variable | Terraform output |
|----------|-----------------|
| `VITE_COGNITO_USER_POOL_ID` | `cognito_user_pool_id` |
| `VITE_COGNITO_APP_CLIENT_ID` | `cognito_app_client_id` |
| `VITE_COGNITO_DOMAIN` | `cognito_hosted_ui_url` (domain portion only, no `https://`) |
| `VITE_API_BASE_URL` | `backend_api_url` |

Retrieve values with:
terraform -chdir=infrastructure output

### 3. Start the dev server

npm run dev

App is available at http://localhost:5173

### 4. Build for production

npm run build
# Output in dist/

### 5. Deploy to S3 + CloudFront

npm run build
aws s3 sync dist/ s3://BUCKET_NAME/ --delete
aws cloudfront create-invalidation --distribution-id DIST_ID --paths "/*"

Replace BUCKET_NAME and DIST_ID with values from `terraform output`.

## Getting a JWT for manual API testing

1. Start the dev server (`npm run dev`).
2. Navigate to http://localhost:5173/login and complete the sign-in flow.
3. After redirect, open browser DevTools → Application → Local Storage.
4. Look for the key matching `CognitoIdentityServiceProvider.*.idToken`.
5. Copy the value and use it in curl:

curl -H "Authorization: Bearer <id_token>" https://<API_URL>/me

## Running tests

npm run test:e2e      # Playwright smoke tests (requires running dev server)

## Project structure

src/
  api/           # apiCall() client wrapper
  contexts/      # AuthContext
  components/    # RequireAuth, Header, RedeemButton, ApprovalPanel, RejectModal
  pages/         # Login, Callback, Dashboard, MyRequests, RequestSubmit, PendingApprovals
tests/           # Playwright specs
```

### 4. Demo script — `implementation/spa/DEMO_SCRIPT.md`

Write a 2–3 page walkthrough that a non-technical observer can follow during a live demo. Keep the language plain; avoid jargon.

```markdown
# Live Demo Script — Zero Trust Data Exchange

**Duration:** ~10 minutes  
**Audience:** Academic supervisors, non-technical evaluators  
**Goal:** Show the end-to-end journey of a sensitive data access request under a zero-trust model.

---

## Scene 1 — "The Requester wants access to data" (~3 min)

> "Imagine a researcher at Organisation A needs access to a sensitive dataset owned by Organisation B.
> In a traditional system they might email a request or fill in a paper form. Here, the process is
> digital, auditable, and requires cryptographic identity verification at every step."

1. Open the SPA at [URL]. Show the login screen.
2. Click **Sign in as Requester (IDP-A)**.
   > "The system redirects to Organisation A's identity provider. No passwords are stored in our system."
3. Log in with the IDP-A test account.
   > "After login, Cognito issues a signed JWT token. Every API call carries this token — the backend
   > never trusts the caller without verifying it."
4. Show the header: user name and organisation displayed.
5. Click **+ New Request**. Fill in the form.
   > "The requester specifies exactly which files they need and why. This creates an immutable audit record."
6. Submit. Show the request in the list with status **PendingOwnerApproval**.

---

## Scene 2 — "The Data Owner reviews and approves" (~4 min)

> "The request now sits with Organisation B's data owner. They receive no automatic access — they must
> actively review and verify their identity before approving."

1. Open a second browser window. Sign in as the IDP-B data owner.
2. Show the Pending Approvals dashboard. Highlight the request from Scene 1.
3. Click **Review**. Walk through the request details panel.
   > "The approver can see exactly who is asking, for what purpose, and which specific files are requested."
4. Click **Send verification code**.
   > "This triggers a one-time code sent to the approver's registered email — a second factor beyond their
   > login credentials. The backend generates and stores a time-limited, hashed code."
5. Check the email (show inbox). Copy the 6-digit code.
6. Enter the code and click **Approve Request**.
   > "The backend validates the OTP, records the approval with a timestamp and the approver's identity,
   > and transitions the request state. All of this is logged to CloudWatch."
7. Show the request disappearing from the pending list.

---

## Scene 3 — "The Requester downloads the approved data" (~2 min)

> "Back in Organisation A — the researcher's request is now approved. But they still don't have
> unrestricted access to the S3 bucket. They get a time-limited, scoped download link."

1. Switch back to the IDP-A browser window. Show the request status updated to **Approved**.
2. Click **⬇ Download**.
   > "The backend generates a pre-signed S3 URL — valid for a short window, scoped to exactly the
   > files that were requested. No standing access, no shared credentials."
3. Show the file downloading in a new tab.

---

## Wrap-up

> "Every action in this demo — login, request submission, OTP verification, approval, and data access —
> is recorded in an immutable audit log. No step can be skipped, and no identity is assumed to be
> trusted without verification. That is zero trust in practice."
```

### 5. Cross-browser scope

Test in Chrome (Chromium) and Firefox only. Safari is explicitly out of scope for this prototype. Document this in `TESTING.md` under a "Scope" heading.

## Definition of Done

- [ ] `implementation/spa/TESTING.md` written with TC-01 through TC-05 test cases
- [ ] Manual test script executed end-to-end without failures in the deployed environment
- [ ] `implementation/spa/tests/smoke.spec.js` created with health check, login page, and redirect guard tests
- [ ] `npx playwright test` passes for Chromium and Firefox
- [ ] `implementation/spa/README.md` written with prerequisites, setup steps, env variable table, and JWT capture instructions
- [ ] `implementation/spa/DEMO_SCRIPT.md` written with three scenes covering the full happy path
- [ ] Cross-browser scope (Chrome + Firefox only) documented in `TESTING.md`
- [ ] `.env.example` referenced in README and committed to the repository
