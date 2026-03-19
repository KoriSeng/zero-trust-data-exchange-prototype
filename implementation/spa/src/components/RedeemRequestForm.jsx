import { useState } from 'react';
import apiCall from '../api/client';

function canRedeem(status) {
  return status === 'OtpSent' || status === 'Approved';
}

export default function RedeemRequestForm({ request, onRedeemed }) {
  const [otp, setOtp] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [redemptionResult, setRedemptionResult] = useState(null);

  if (!canRedeem(request.status)) {
    return <span className="muted-text">—</span>;
  }

  async function handleRedeem(event) {
    event.preventDefault();
    setError('');

    if (!otp.trim()) {
      setError('Enter the 6-digit OTP from the approver.');
      return;
    }

    setIsSubmitting(true);

    try {
      const result = await apiCall(`/requests/${request.id}/redeem`, {
        method: 'POST',
        body: JSON.stringify({ otp: otp.trim() }),
      });

      setRedemptionResult(result);
      setOtp('');

      if (typeof onRedeemed === 'function') {
        await onRedeemed();
      }
    } catch (redeemError) {
      setError(redeemError.message ?? 'Could not redeem request.');
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="redeem-panel">
      <form className="inline-form" onSubmit={handleRedeem}>
        <label className="sr-only" htmlFor={`otp-${request.id}`}>
          One-time password
        </label>
        <input
          id={`otp-${request.id}`}
          type="text"
          value={otp}
          maxLength={6}
          inputMode="numeric"
          onChange={(event) => setOtp(event.target.value.replace(/\D/g, ''))}
          placeholder="OTP"
        />
        <button type="submit" className="secondary-button" disabled={isSubmitting || otp.length !== 6}>
          {isSubmitting ? 'Redeeming…' : 'Redeem'}
        </button>
      </form>

      {error && <p className="error-inline">{error}</p>}

      {redemptionResult?.presignedUrls && (
        <details className="link-details">
          <summary>View generated download links</summary>
          <ul>
            {Object.entries(redemptionResult.presignedUrls).map(([key, url]) => (
              <li key={key}>
                <a href={url} target="_blank" rel="noreferrer">
                  {key}
                </a>
              </li>
            ))}
          </ul>
        </details>
      )}
    </div>
  );
}
