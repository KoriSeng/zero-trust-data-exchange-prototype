# Manual Test Script — Zero Trust Data Exchange SPA

## Scope

- Browsers in scope: **Chromium/Chrome** and **Firefox**.
- Safari and mobile browsers are out of scope for this prototype.

## Prerequisites

- Two browser sessions (separate profiles/incognito windows)
- Valid IDP-A account (Requester)
- Valid IDP-B account (Data Owner)
- SPA available on `http://localhost:5173` or deployed CloudFront URL
- Backend API reachable from `VITE_API_BASE_URL`

---

## TC-01 — Requester login and request submission

1. Open `/login`.
2. Click **Sign in as Requester (IDP-A)**.
3. Complete Hosted UI + IdP login.
4. Confirm redirect through `/callback` to dashboard.
5. Open **New Request**.
6. Submit form using:
   - Dataset ID: `DS-001`
   - Dataset Name: `Test Dataset`
   - Purpose: `Academic research`
   - Object Keys: `data/sample.csv`
   - Data Owner Org: `ORG-B`
7. Confirm success banner on **My Requests** with request ID.
8. Confirm status is `Submitted` or `PendingOwnerApproval`.

---

## TC-02 — Data owner review and approval (OTP issue)

1. Open second browser session.
2. Sign in with **Sign in as Data Owner (IDP-B)**.
3. Open **Pending Approvals**.
4. Find request from TC-01 and click **Review**.
5. Click **Generate OTP preview**.
6. Confirm debug panel shows simulated email content and OTP code.
7. Click **Approve request** using the shown code.
8. Confirm request is removed from pending list.
9. Copy OTP for requester redemption.

---

## TC-03 — Requester redemption and download

1. Return to requester session.
2. Open **My Requests** and wait for status to become `OtpSent`.
3. Enter OTP in the request row and click **Redeem**.
4. Confirm browser opens a pre-signed URL in a new tab.
5. Confirm generated links are visible in the row details.

---

## TC-04 — Rejection flow

1. Submit another request as requester.
2. As data owner, open **Pending Approvals**.
3. Click **Reject** for that request.
4. Enter reason: `Insufficient justification`.
5. Click **Confirm rejection**.
6. Verify request is removed from pending list.
7. Back in requester session, refresh **My Requests**.
8. Confirm rejected request status is `Denied`.

---

## TC-05 — Invalid OTP handling

1. Submit and approve a new request to issue OTP.
2. In requester session, enter `000000` as OTP.
3. Click **Redeem**.
4. Confirm inline error message is displayed.
5. Enter correct OTP and confirm redemption succeeds.

---

## Expected evidence to capture

- Login page showing both IdP buttons
- Request submission success message with request ID
- Pending approval card before/after approval
- OTP issued message (with masked code in final report if needed)
- Redeem success and opened pre-signed URL
- Rejection reason and resulting `Denied` status
- Invalid OTP error message
