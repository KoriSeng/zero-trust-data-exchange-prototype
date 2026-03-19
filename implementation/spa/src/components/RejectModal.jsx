import { useState } from 'react';
import apiCall from '../api/client';

export default function RejectModal({ requestId, onRejected, onCancel }) {
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleReject() {
    setError('');
    if (!reason.trim()) {
      setError('A rejection reason is required.');
      return;
    }

    setIsSubmitting(true);

    try {
      await apiCall(`/requests/${requestId}/deny`, {
        method: 'POST',
        body: JSON.stringify({ reason: reason.trim() }),
      });

      if (typeof onRejected === 'function') {
        onRejected();
      }
    } catch (rejectError) {
      setError(rejectError.message ?? 'Could not reject request.');
      setIsSubmitting(false);
    }
  }

  return (
    <div className="modal-overlay" role="presentation">
      <div className="modal" role="dialog" aria-modal="true" aria-labelledby="reject-request-title">
        <h3 id="reject-request-title">Reject request</h3>

        <label>
          Reason
          <textarea
            rows={3}
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            placeholder="Explain why this request is being rejected"
          />
        </label>

        {error && <p className="error-inline">{error}</p>}

        <div className="button-row">
          <button type="button" className="danger-button" onClick={handleReject} disabled={isSubmitting}>
            {isSubmitting ? 'Rejecting…' : 'Confirm rejection'}
          </button>
          <button type="button" className="secondary-button" onClick={onCancel}>
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}
