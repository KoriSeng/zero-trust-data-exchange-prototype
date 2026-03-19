---
type: Task
task_id: TASK-032
title: "Build Approval Workflow Interface with OTP"
owner: STK-001
status: "In Progress"
related_milestone: MS-009
---

## Description

Build the DataOwner-facing SPA page that lists pending approval requests, walks the approver through a two-step OTP verification flow (send code → enter code → approve), and supports rejection with a reason. Role-gating ensures only IDP-B users (DataOwner role) see this page.

## Implementation Notes

### 1. Page — `src/pages/PendingApprovals.jsx`

Load pending requests on mount and render a card/row for each one. Each entry expands into an inline approval panel.

```jsx
import { useEffect, useState } from 'react';
import apiCall from '../api/client';
import ApprovalPanel from '../components/ApprovalPanel';
import RejectModal   from '../components/RejectModal';

export default function PendingApprovals() {
  const [requests, setRequests] = useState([]);
  const [expanded, setExpanded] = useState(null);   // requestId currently open
  const [rejecting, setRejecting] = useState(null); // requestId being rejected

  useEffect(() => { loadRequests(); }, []);

  async function loadRequests() {
    const data = await apiCall('/requests/pending');
    setRequests(data);
  }

  function removeRequest(id) {
    // Optimistic update — remove immediately after action
    setRequests(prev => prev.filter(r => r.requestId !== id));
    setExpanded(null);
    setRejecting(null);
  }

  if (requests.length === 0) return <p>No pending requests.</p>;

  return (
    <div>
      <h2>Pending Approval Requests</h2>
      {requests.map(r => (
        <div key={r.requestId} className="request-card">
          <div className="request-summary">
            <span><strong>{r.datasetName}</strong> — {r.requesterEmail}</span>
            <span>{new Date(r.createdAt).toLocaleDateString()}</span>
            <div>
              <button onClick={() => setExpanded(expanded === r.requestId ? null : r.requestId)}>
                {expanded === r.requestId ? 'Collapse' : 'Review'}
              </button>
              <button className="btn-danger" onClick={() => setRejecting(r.requestId)}>
                Reject
              </button>
            </div>
          </div>

          {expanded === r.requestId && (
            <ApprovalPanel request={r} onApproved={() => removeRequest(r.requestId)} />
          )}
        </div>
      ))}

      {rejecting && (
        <RejectModal
          requestId={rejecting}
          onRejected={() => removeRequest(rejecting)}
          onCancel={() => setRejecting(null)}
        />
      )}
    </div>
  );
}
```

### 2. Approval panel — `src/components/ApprovalPanel.jsx`

The four-step inline flow: review details → send OTP → enter code → approve.

```jsx
import { useState } from 'react';
import apiCall from '../api/client';

export default function ApprovalPanel({ request, onApproved }) {
  // Step: 'review' | 'otp_sent' | 'approving'
  const [step,         setStep]         = useState('review');
  const [otpCode,      setOtpCode]      = useState('');
  const [comments,     setComments]     = useState('');
  const [otpError,     setOtpError]     = useState(null);
  const [sendingOtp,   setSendingOtp]   = useState(false);
  const [approving,    setApproving]    = useState(false);

  async function sendOtp() {
    setSendingOtp(true);
    setOtpError(null);
    try {
      await apiCall(`/requests/${request.requestId}/otp/send`, { method: 'POST' });
      setStep('otp_sent');
    } catch (err) {
      setOtpError(`Failed to send code: ${err.message}`);
    } finally {
      setSendingOtp(false);
    }
  }

  async function approve() {
    if (!otpCode.match(/^\d{6}$/)) {
      setOtpError('Please enter a valid 6-digit code.');
      return;
    }
    setApproving(true);
    setOtpError(null);
    try {
      await apiCall(`/requests/${request.requestId}/approve`, {
        method: 'POST',
        body:   JSON.stringify({ approvalCode: otpCode, comments })
      });
      onApproved();
    } catch (err) {
      // The backend returns remaining attempts in the error message (MS-008)
      setOtpError(err.message);
      setApproving(false);
    }
  }

  return (
    <div className="approval-panel">
      {/* Step 1 — always visible: request details */}
      <section>
        <h3>Request Details</h3>
        <dl>
          <dt>Requester</dt>   <dd>{request.requesterEmail}</dd>
          <dt>Dataset</dt>     <dd>{request.datasetName} ({request.datasetId})</dd>
          <dt>Purpose</dt>     <dd>{request.purpose}</dd>
          <dt>Object Keys</dt> <dd>{(request.objectKeys ?? []).join(', ')}</dd>
          <dt>Submitted</dt>   <dd>{new Date(request.createdAt).toLocaleString()}</dd>
        </dl>
      </section>

      {/* Step 2 — send OTP */}
      {step === 'review' && (
        <section>
          <p>To approve this request, you must verify your identity with a one-time code.</p>
          <button onClick={sendOtp} disabled={sendingOtp}>
            {sendingOtp ? 'Sending…' : 'Send verification code to my email'}
          </button>
          {otpError && <p className="error">{otpError}</p>}
        </section>
      )}

      {/* Step 3 — enter OTP and optional comments, then approve */}
      {step === 'otp_sent' && (
        <section>
          <p className="success">✓ Verification code sent to your email.</p>

          <label>6-digit code
            <input
              type="text"
              inputMode="numeric"
              maxLength={6}
              value={otpCode}
              onChange={e => setOtpCode(e.target.value.replace(/\D/g, ''))}
              placeholder="123456"
            />
          </label>

          <label>Comments (optional)
            <textarea value={comments} onChange={e => setComments(e.target.value)} />
          </label>

          {otpError && <p className="error">{otpError}</p>}

          <button onClick={approve} disabled={approving || otpCode.length !== 6}>
            {approving ? 'Approving…' : 'Approve Request'}
          </button>
          <button onClick={sendOtp} disabled={sendingOtp} className="btn-secondary">
            Resend code
          </button>
        </section>
      )}
    </div>
  );
}
```

**OTP error display:** The backend returns a message like `"Invalid or expired code. 2 attempts remaining."` — surface that string directly from `err.message` so the approver knows how many tries are left.

### 3. Reject modal — `src/components/RejectModal.jsx`

A lightweight inline modal (no library required for a POC):

```jsx
import { useState } from 'react';
import apiCall from '../api/client';

export default function RejectModal({ requestId, onRejected, onCancel }) {
  const [reason,    setReason]    = useState('');
  const [rejecting, setRejecting] = useState(false);
  const [error,     setError]     = useState(null);

  async function handleReject() {
    if (!reason.trim()) { setError('A reason is required.'); return; }
    setRejecting(true);
    try {
      await apiCall(`/requests/${requestId}/reject`, {
        method: 'POST',
        body:   JSON.stringify({ reason: reason.trim() })
      });
      onRejected();
    } catch (err) {
      setError(err.message);
      setRejecting(false);
    }
  }

  return (
    <div className="modal-overlay">
      <div className="modal">
        <h3>Reject Request</h3>
        <label>Reason for rejection
          <textarea value={reason} onChange={e => setReason(e.target.value)} rows={3} />
        </label>
        {error && <p className="error">{error}</p>}
        <div>
          <button className="btn-danger" onClick={handleReject} disabled={rejecting}>
            {rejecting ? 'Rejecting…' : 'Confirm Rejection'}
          </button>
          <button onClick={onCancel}>Cancel</button>
        </div>
      </div>
    </div>
  );
}
```

### 4. Optimistic list updates

After a successful approve or reject, `removeRequest(id)` filters the request out of local state immediately. No re-fetch needed — the action is final. This keeps the UX snappy without requiring WebSockets.

### 5. Role gate

`PendingApprovals` should only render for DataOwner users. The Dashboard already handles this (see TASK-031, note 1). As a belt-and-braces measure, add a guard at the top of `PendingApprovals`:

```jsx
const { user } = useAuth();
if (!user?.roles?.includes('DataOwner')) return <p>Access denied.</p>;
```

### 6. Route wiring

Add to `App.jsx`:

```jsx
<Route path="/approvals" element={<RequireAuth><PendingApprovals /></RequireAuth>} />
```

### 7. File structure after this task

```
src/
  pages/
    PendingApprovals.jsx
  components/
    ApprovalPanel.jsx
    RejectModal.jsx
```

## Definition of Done

- [ ] `PendingApprovals` page loads `GET /requests/pending` and renders a row per request
- [ ] Only users with the `DataOwner` role can see this page
- [ ] "Send verification code" calls `POST /requests/{id}/otp/send` and shows a success message
- [ ] OTP input accepts exactly 6 digits
- [ ] "Approve" button calls `POST /requests/{id}/approve` with `{ approvalCode, comments }`
- [ ] Invalid/expired OTP error message (including attempts remaining) is shown inline
- [ ] "Reject" button opens the reject modal and calls `POST /requests/{id}/reject` with a reason
- [ ] Approved and rejected requests are removed from the list immediately (optimistic update)
- [ ] API errors are displayed inline; the page does not crash on network failure
