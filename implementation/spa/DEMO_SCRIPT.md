# Live Demo Script — Zero Trust Data Exchange

**Duration:** 10–12 minutes  
**Audience:** Academic supervisors and non-technical assessors

## Demo objective

Show a complete secure data access journey:

1. Requester asks for data.
2. Data owner reviews and approves/rejects.
3. Requester redeems OTP for time-limited access.

---

## Scene 1 — Requester submits an access request (3–4 min)

1. Open the SPA login page.
2. Explain there are two identity providers:
   - IDP-A for requesters
   - IDP-B for data owners
3. Click **Sign in as Requester (IDP-A)** and complete login.
4. Show dashboard user/org details loaded from `/me`.
5. Open **New Request**.
6. Fill in dataset, purpose, and object keys.
7. Submit request and show request ID in **My Requests**.

**Narration:**
“The platform never grants access immediately. Every request is explicit, attributable, and auditable.”

---

## Scene 2 — Data owner approves with OTP issuance (4–5 min)

1. Open second browser session.
2. Sign in using **Sign in as Data Owner (IDP-B)**.
3. Navigate to **Pending Approvals** and open the request.
4. Explain requester, dataset, purpose, and object key review details.
5. Click **Approve and send OTP**.
6. Show OTP in the confirmation banner.

**Narration:**
“Approval is controlled by the data owner, and a short-lived OTP becomes the second verification factor before data access can be redeemed.”

---

## Scene 3 — Requester redeems OTP for temporary download (2–3 min)

1. Return to requester session.
2. Refresh **My Requests** until status is `OtpSent`.
3. Enter OTP and click **Redeem**.
4. Show opened pre-signed URL and generated links.

**Narration:**
“The requester receives only short-lived, scoped URLs—not broad bucket access. This enforces least privilege and time-bound access.”

---

## Optional extension — Rejection path (1–2 min)

1. Submit a second request with weak purpose.
2. As data owner, reject with reason.
3. Show requester sees `Denied` status.

**Narration:**
“The same workflow supports transparent denial decisions, also captured in audit records.”

---

## Closing message

“This prototype demonstrates zero-trust principles in practice: federated identity, explicit approval, OTP-gated redemption, and traceable request lifecycle states.”
