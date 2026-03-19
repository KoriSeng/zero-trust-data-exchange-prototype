const STATUS_STYLE_MAP = {
  Submitted: 'badge badge-submitted',
  PendingAdminReview: 'badge badge-review',
  PendingOwnerApproval: 'badge badge-pending',
  ClaimPending: 'badge badge-otp',
  Approved: 'badge badge-approved',
  OtpSent: 'badge badge-otp',
  Redeemed: 'badge badge-redeemed',
  Completed: 'badge badge-completed',
  Denied: 'badge badge-denied',
  Expired: 'badge badge-expired',
  Revoked: 'badge badge-denied',
};

const STATUS_LABEL_MAP = {
  PendingAdminReview: 'Pending admin review',
  PendingOwnerApproval: 'Pending owner approval',
  ClaimPending: 'Claim pending',
  OtpSent: 'OTP sent',
};

export default function StatusBadge({ status }) {
  return (
    <span className={STATUS_STYLE_MAP[status] ?? 'badge badge-default'}>
      {STATUS_LABEL_MAP[status] ?? status}
    </span>
  );
}
