import { useState } from 'react';
import apiCall from '../api/client';

export default function ApprovalPanel({ request, onApproved }) {
  const [comments, setComments] = useState('');
  const [accessDurationHours, setAccessDurationHours] = useState('24');
  const [approvalCode, setApprovalCode] = useState('');
  const [otpPreview, setOtpPreview] = useState(null);
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSendingOtp, setIsSendingOtp] = useState(false);

  async function handleSendOtp() {
    setError('');
    setIsSendingOtp(true);

    try {
      const result = await apiCall(`/requests/${request.id}/otp/send`, {
        method: 'POST',
      });
      setOtpPreview(result?.debugEmail ?? null);
      if (result?.debugEmail?.otpCode) {
        setApprovalCode(result.debugEmail.otpCode);
      }
    } catch (sendError) {
      setError(sendError.message ?? 'Could not generate OTP preview.');
    } finally {
      setIsSendingOtp(false);
    }
  }

  async function handleApprove(event) {
    event.preventDefault();
    setError('');

    if (!approvalCode.trim()) {
      setError('Generate OTP preview first, then use the 6-digit code to approve.');
      return;
    }

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
    payload.approvalCode = approvalCode.trim();

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
        <div className="button-row">
          <button
            type="button"
            className="secondary-button"
            onClick={handleSendOtp}
            disabled={isSendingOtp || isSubmitting}
          >
            {isSendingOtp ? 'Generating OTP…' : 'Generate OTP preview'}
          </button>
        </div>

        {otpPreview && (
          <div className="otp-debug-panel">
            <p>
              <strong>Debug OTP email preview</strong> (no real email sent)
            </p>
            <p>
              To (locked): <strong>{otpPreview.to}</strong>
            </p>
            <p>
              Subject (locked): <strong>{otpPreview.subject}</strong>
            </p>
            <p>
              OTP code: <strong>{otpPreview.otpCode}</strong>
            </p>
            {otpPreview.expiresAt && <p>Expires at: {new Date(otpPreview.expiresAt).toLocaleString()}</p>}
            <pre>{otpPreview.textBody}</pre>
          </div>
        )}

        <label>
          Approval code (from debug preview, locked once generated)
          <input
            value={approvalCode}
            maxLength={6}
            onChange={(event) => {
              if (!otpPreview) {
                setApprovalCode(event.target.value.replace(/[^\d]/g, ''));
              }
            }}
            placeholder="6-digit OTP"
            readOnly={Boolean(otpPreview)}
          />
        </label>

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
          Approving this request issues a one-time code (OTP) for the requester.
        </p>

        {error && <p className="error-inline">{error}</p>}

        <button type="submit" className="primary-button" disabled={isSubmitting}>
          {isSubmitting ? 'Approving…' : 'Approve request'}
        </button>
      </form>
    </div>
  );
}
