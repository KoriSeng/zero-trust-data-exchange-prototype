import { useEffect } from 'react';

export default function OtpEmailModal({ isOpen, onClose, otpEmailPreview }) {
  useEffect(() => {
    if (!isOpen) return;

    const handleEscape = (event) => {
      if (event.key === 'Escape') {
        onClose();
      }
    };

    document.addEventListener('keydown', handleEscape);
    return () => document.removeEventListener('keydown', handleEscape);
  }, [isOpen, onClose]);

  useEffect(() => {
    if (isOpen) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = '';
    }
    return () => {
      document.body.style.overflow = '';
    };
  }, [isOpen]);

  if (!isOpen) return null;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content otp-email-modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3>OTP Email Preview</h3>
          <button type="button" className="modal-close-button" onClick={onClose} aria-label="Close">
            <svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor">
              <path
                fillRule="evenodd"
                d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z"
                clipRule="evenodd"
              />
            </svg>
          </button>
        </div>

        <div className="modal-body">
          <div className="otp-email-details">
            <div className="otp-field">
              <label>To:</label>
              <span>{otpEmailPreview.to}</span>
            </div>
            <div className="otp-field">
              <label>Subject:</label>
              <span>{otpEmailPreview.subject}</span>
            </div>
            <div className="otp-field otp-code-highlight">
              <label>OTP Code:</label>
              <span className="otp-code">{otpEmailPreview.otpCode}</span>
            </div>
            {otpEmailPreview.expiresAt && (
              <div className="otp-field">
                <label>Expires:</label>
                <span>{new Date(otpEmailPreview.expiresAt).toLocaleString()}</span>
              </div>
            )}
          </div>

          <div className="otp-email-body">
            <label>Email Content:</label>
            <pre>{otpEmailPreview.textBody}</pre>
          </div>

          <div className="otp-prototype-notice">
            <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
              <path
                fillRule="evenodd"
                d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z"
                clipRule="evenodd"
              />
            </svg>
            <span>This preview is for testing purposes (prototype mode)</span>
          </div>
        </div>

        <div className="modal-footer">
          <button type="button" className="primary-button" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
}
