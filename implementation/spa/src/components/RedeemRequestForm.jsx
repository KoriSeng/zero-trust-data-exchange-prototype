import { useState } from 'react';
import apiCall from '../api/client';

function canRedeem(status) {
  return (
    status === 'OtpSent' ||
    status === 'ClaimPending' ||
    status === 'Approved' ||
    status === 'PendingOwnerApproval'
  );
}

function canDownload(status) {
  return status === 'Redeemed';
}

export default function RedeemRequestForm({ request, onRedeemed }) {
  const [otp, setOtp] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [redemptionResult, setRedemptionResult] = useState(null);
  const [otpEmailPreview, setOtpEmailPreview] = useState(null);
  const [isLoadingOtpEmail, setIsLoadingOtpEmail] = useState(false);
  const [downloadError, setDownloadError] = useState('');
  const [downloadingKey, setDownloadingKey] = useState('');
  const [lastDownloadExpiry, setLastDownloadExpiry] = useState('');
  const [showDownloadFiles, setShowDownloadFiles] = useState(false);

  if (!canRedeem(request.status) && !canDownload(request.status)) {
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

  async function handleViewOtpEmail() {
    setError('');
    setIsLoadingOtpEmail(true);
    try {
      const result = await apiCall(`/requests/${request.id}/otp/email`);
      setOtpEmailPreview(result);
      if (result?.otpCode) {
        setOtp(result.otpCode);
      }
    } catch (loadError) {
      setError(loadError.message ?? 'Could not load OTP email preview.');
    } finally {
      setIsLoadingOtpEmail(false);
    }
  }

  async function handleDownloadKey(objectKey) {
    setDownloadError('');
    setDownloadingKey(objectKey);
    try {
      const params = new URLSearchParams({ key: objectKey });
      const result = await apiCall(`/requests/${request.id}/download-url?${params.toString()}`);
      setLastDownloadExpiry(result?.expiresAt ?? '');
      if (result?.url) {
        window.location.assign(result.url);
      } else {
        setDownloadError('Download URL was not returned by the server.');
      }
    } catch (loadError) {
      setDownloadError(loadError.message ?? 'Could not generate download link.');
    } finally {
      setDownloadingKey('');
    }
  }

  return (
    <div className="redeem-panel">
      {canRedeem(request.status) && (
        <div className="button-row">
          <button type="button" className="secondary-button" onClick={handleViewOtpEmail} disabled={isLoadingOtpEmail}>
            {isLoadingOtpEmail ? 'Loading OTP email…' : 'View OTP Email'}
          </button>
        </div>
      )}

      {canRedeem(request.status) && otpEmailPreview && (
        <div className="otp-debug-panel">
          <p>
            <strong>Debug OTP email preview</strong> (prototype mode)
          </p>
          <p>
            To: <strong>{otpEmailPreview.to}</strong>
          </p>
          <p>
            Subject: <strong>{otpEmailPreview.subject}</strong>
          </p>
          <p>
            OTP code: <strong>{otpEmailPreview.otpCode}</strong>
          </p>
          {otpEmailPreview.expiresAt && <p>Expires at: {new Date(otpEmailPreview.expiresAt).toLocaleString()}</p>}
          <pre>{otpEmailPreview.textBody}</pre>
        </div>
      )}

      {canRedeem(request.status) && (
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
      )}

      {error && <p className="error-inline">{error}</p>}

      {canDownload(request.status) && (
        <div className="stack-gap">
          <div className="button-row">
            <button type="button" className="secondary-button" onClick={() => setShowDownloadFiles((prev) => !prev)}>
              {showDownloadFiles ? 'Hide files' : 'View files'}
            </button>
          </div>
          {showDownloadFiles && (
            <div className="button-row">
              {(request.objectKeys ?? []).map((objectKey) => (
                <button
                  key={objectKey}
                  type="button"
                  className="secondary-button"
                  onClick={() => handleDownloadKey(objectKey)}
                  disabled={Boolean(downloadingKey) && downloadingKey === objectKey}
                >
                  {downloadingKey === objectKey ? 'Generating link…' : `Download ${objectKey.split('/').pop() ?? objectKey}`}
                </button>
              ))}
            </div>
          )}
          {lastDownloadExpiry && <p className="muted-text">Download link expires at {new Date(lastDownloadExpiry).toLocaleTimeString()}.</p>}
          {downloadError && <p className="error-inline">{downloadError}</p>}
        </div>
      )}

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
