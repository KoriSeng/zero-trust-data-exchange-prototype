import { useState } from 'react';
import apiCall from '../api/client';

export default function ApprovalPanel({ request, onApproved }) {
  const [comments, setComments] = useState('');
  const [accessDurationHours, setAccessDurationHours] = useState('24');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleApprove(event) {
    event.preventDefault();
    setError('');

    let parsedHours;
    if (accessDurationHours.trim()) {
      parsedHours = Number.parseInt(accessDurationHours, 10);
      if (Number.isNaN(parsedHours) || parsedHours <= 0) {
        setError('Access duration must be a positive integer.');
        return;
      }
    }

    const payload = {
      approved: true,
    };

    if (comments.trim()) {
      payload.comments = comments.trim();
    }

    if (parsedHours) {
      payload.accessDurationHours = parsedHours;
    }

    setIsSubmitting(true);

    try {
      const result = await apiCall(`/requests/${request.id}/approve`, {
        method: 'POST',
        body: JSON.stringify(payload),
      });

      if (typeof onApproved === 'function') {
        onApproved(result);
      }
    } catch (approveError) {
      setError(approveError.message ?? 'Approval failed.');
      setIsSubmitting(false);
    }
  }

  return (
    <div className="approval-panel">
      <div className="details-grid">
        <div>
          <strong>Requester</strong>
          <p>{request.requesterEmail}</p>
        </div>
        <div>
          <strong>Dataset</strong>
          <p>
            {request.datasetName ?? request.datasetId} ({request.datasetId})
          </p>
        </div>
        <div>
          <strong>Purpose</strong>
          <p>{request.purpose}</p>
        </div>
      </div>

      <label>
        Full user agreement (read-only)
        <textarea
          rows={8}
          value={request.agreementContent ?? 'No agreement content captured for this request.'}
          readOnly
        />
      </label>

      <form className="stack-form" onSubmit={handleApprove}>
        <label>
          Approver comments (optional)
          <textarea
            value={comments}
            rows={3}
            onChange={(event) => setComments(event.target.value)}
            placeholder="Reason for approval or constraints"
          />
        </label>

        <label>
          Access duration (hours, optional)
          <input
            type="number"
            min={1}
            value={accessDurationHours}
            onChange={(event) => setAccessDurationHours(event.target.value)}
          />
        </label>

        <p className="muted-text">
          Approving this request advances the workflow. OTP is generated after approval and shown to the requester in My Requests.
        </p>

        {error && <p className="error-inline">{error}</p>}

        <button type="submit" className="primary-button" disabled={isSubmitting}>
          {isSubmitting ? 'Approving…' : 'Approve request'}
        </button>
      </form>
    </div>
  );
}
