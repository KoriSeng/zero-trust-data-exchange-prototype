---
type: Task
task_id: TASK-031
title: "Build Request Submission Interface"
owner: STK-001
status: "In Progress"
related_milestone: MS-009
---

## Description

Build the Requester-facing SPA pages: a form to submit a new data access request (`POST /requests`), a list view of the requester's own requests (`GET /requests/my`) with live status badges, and a "Download" button on approved requests that redeems a pre-signed S3 URL (`POST /requests/{id}/redeem`).

## Implementation Notes

### 1. Dashboard routing by role

The Dashboard page reads the user's `roles` (from `GET /me`) and renders the correct sub-page:

```jsx
// src/pages/Dashboard.jsx
import { useEffect, useState } from 'react';
import apiCall from '../api/client';
import MyRequests     from './MyRequests';
import PendingApprovals from './PendingApprovals'; // TASK-032

export default function Dashboard() {
  const [me, setMe] = useState(null);

  useEffect(() => {
    apiCall('/me').then(setMe).catch(() => {});
  }, []);

  if (!me) return <p>Loading…</p>;

  const isRequester  = me.roles?.includes('Requester');
  const isDataOwner  = me.roles?.includes('DataOwner');

  return (
    <div>
      {isRequester  && <MyRequests />}
      {isDataOwner  && <PendingApprovals />}
    </div>
  );
}
```

### 2. Request submission form — `src/pages/RequestSubmit.jsx`

This page is linked from `MyRequests`. It is a simple controlled form — no external form library needed for a POC.

```jsx
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import apiCall from '../api/client';

export default function RequestSubmit() {
  const navigate = useNavigate();
  const [form, setForm] = useState({
    datasetId:    '',
    datasetName:  '',
    purpose:      '',
    objectKeys:   '',   // comma-separated input, split before submit
    dataOwnerOrg: 'ORG-B'
  });
  const [error,      setError]      = useState(null);
  const [submitting, setSubmitting] = useState(false);

  function handleChange(e) {
    setForm({ ...form, [e.target.name]: e.target.value });
  }

  async function handleSubmit(e) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const body = {
        datasetId:    form.datasetId.trim(),
        datasetName:  form.datasetName.trim(),
        purpose:      form.purpose.trim(),
        objectKeys:   form.objectKeys.split(',').map(k => k.trim()).filter(Boolean),
        dataOwnerOrg: form.dataOwnerOrg
      };
      const result = await apiCall('/requests', {
        method: 'POST',
        body:   JSON.stringify(body)
      });
      // Navigate to request list; pass requestId via state for confirmation banner
      navigate('/requests', { state: { newRequestId: result.requestId } });
    } catch (err) {
      setError(err.message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={handleSubmit}>
      <h2>Submit Data Access Request</h2>
      {error && <p className="error">{error}</p>}

      <label>Dataset ID
        <input name="datasetId" value={form.datasetId} onChange={handleChange} required />
      </label>

      <label>Dataset Name
        <input name="datasetName" value={form.datasetName} onChange={handleChange} required />
      </label>

      <label>Purpose
        <textarea name="purpose" value={form.purpose} onChange={handleChange} required />
      </label>

      <label>Object Keys (comma-separated)
        <input name="objectKeys" value={form.objectKeys} onChange={handleChange}
               placeholder="folder/file1.csv, folder/file2.csv" required />
      </label>

      <label>Data Owner Organisation
        <select name="dataOwnerOrg" value={form.dataOwnerOrg} onChange={handleChange}>
          <option value="ORG-B">ORG-B</option>
        </select>
      </label>

      <button type="submit" disabled={submitting}>
        {submitting ? 'Submitting…' : 'Submit Request'}
      </button>
    </form>
  );
}
```

> **Optional enhancement:** call `GET /datasets` on mount to populate a dataset picker dropdown instead of free-text fields.

### 3. Request list — `src/pages/MyRequests.jsx`

```jsx
import { useEffect, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import apiCall from '../api/client';

const STATUS_CLASSES = {
  Submitted:             'badge-blue',
  PendingOwnerApproval:  'badge-yellow',
  Approved:              'badge-green',
  Rejected:              'badge-red'
};

export default function MyRequests() {
  const [requests, setRequests] = useState([]);
  const location = useLocation();
  const newId    = location.state?.newRequestId;

  useEffect(() => {
    loadRequests();
  }, []);

  async function loadRequests() {
    const data = await apiCall('/requests/my');
    setRequests(data);
  }

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between' }}>
        <h2>My Requests</h2>
        <Link to="/requests/new"><button>+ New Request</button></Link>
      </div>

      {newId && <p className="success">Request {newId} submitted successfully.</p>}

      <table>
        <thead>
          <tr>
            <th>Request ID</th><th>Dataset</th><th>Purpose</th>
            <th>Status</th><th>Submitted</th><th>Action</th>
          </tr>
        </thead>
        <tbody>
          {requests.map(r => (
            <tr key={r.requestId}>
              <td>{r.requestId}</td>
              <td>{r.datasetName}</td>
              <td>{r.purpose}</td>
              <td><span className={STATUS_CLASSES[r.status] ?? 'badge-grey'}>{r.status}</span></td>
              <td>{new Date(r.createdAt).toLocaleString()}</td>
              <td>
                {r.status === 'Approved' && (
                  <RedeemButton requestId={r.requestId} />
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
```

### 4. Status polling on the request list

Add a simple interval to keep the list fresh while the user is on the page. 10 seconds is fine for a POC — no WebSocket required.

```jsx
useEffect(() => {
  loadRequests();
  const interval = setInterval(loadRequests, 10_000);
  return () => clearInterval(interval);   // cleanup on unmount
}, []);
```

### 5. Redeem (download) button — `src/components/RedeemButton.jsx`

When a request is `Approved`, the requester can generate a pre-signed S3 URL and download the file.

```jsx
import { useState } from 'react';
import apiCall from '../api/client';

export default function RedeemButton({ requestId }) {
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState(null);

  async function handleRedeem() {
    setLoading(true);
    setError(null);
    try {
      const result = await apiCall(`/requests/${requestId}/redeem`, { method: 'POST' });
      // Open pre-signed URL in a new tab
      window.open(result.url, '_blank', 'noopener,noreferrer');
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }

  return (
    <>
      <button onClick={handleRedeem} disabled={loading}>
        {loading ? 'Generating…' : '⬇ Download'}
      </button>
      {error && <span className="error">{error}</span>}
    </>
  );
}
```

### 6. Route wiring

Add the new routes to `App.jsx` (all protected by `RequireAuth`):

```jsx
<Route path="/requests"     element={<RequireAuth><MyRequests /></RequireAuth>} />
<Route path="/requests/new" element={<RequireAuth><RequestSubmit /></RequireAuth>} />
```

### 7. File structure after this task

```
src/
  pages/
    MyRequests.jsx
    RequestSubmit.jsx
  components/
    RedeemButton.jsx
```

## Definition of Done

- [ ] `RequestSubmit` form submits to `POST /requests` with correctly shaped body
- [ ] `objectKeys` string is split into an array before submission
- [ ] On success, user is redirected to `/requests` with a confirmation message showing `requestId`
- [ ] `MyRequests` loads from `GET /requests/my` and renders a status badge per request
- [ ] Status badge colours differ visually for Submitted / PendingOwnerApproval / Approved / Rejected
- [ ] Page auto-refreshes request statuses every 10 seconds while open
- [ ] Approved requests display a "Download" button
- [ ] "Download" button calls `POST /requests/{id}/redeem` and opens the pre-signed URL in a new tab
- [ ] Errors from the API are displayed inline (not silently swallowed)
