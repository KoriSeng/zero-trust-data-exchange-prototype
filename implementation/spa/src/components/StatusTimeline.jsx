const TIMELINE_STEPS = [
  { key: 'Submitted', label: 'Submitted' },
  { key: 'PendingOwnerApproval', label: 'Pending Approval' },
  { key: 'Approved', label: 'Approved' },
  { key: 'ClaimPending', label: 'OTP Available' },
  { key: 'Redeemed', label: 'Access Granted' },
];

const TERMINAL_STATES = ['Denied', 'Expired', 'Completed'];

function getStepStatus(step, currentStatus) {
  const stepIndex = TIMELINE_STEPS.findIndex((s) => s.key === step.key);
  const currentIndex = TIMELINE_STEPS.findIndex((s) => s.key === currentStatus);

  if (TERMINAL_STATES.includes(currentStatus)) {
    return currentStatus === 'Completed' && stepIndex <= 4 ? 'completed' : 'inactive';
  }

  if (stepIndex < currentIndex) return 'completed';
  if (stepIndex === currentIndex) return 'active';
  return 'pending';
}

export default function StatusTimeline({ status }) {
  if (TERMINAL_STATES.includes(status) && status !== 'Completed') {
    return (
      <div className="timeline-terminal">
        <div className={`timeline-terminal-badge ${status === 'Denied' ? 'denied' : 'expired'}`}>
          {status}
        </div>
      </div>
    );
  }

  return (
    <div className="status-timeline">
      {TIMELINE_STEPS.map((step, index) => {
        const stepStatus = getStepStatus(step, status);
        return (
          <div key={step.key} className="timeline-step">
            <div className={`timeline-dot ${stepStatus}`}>
              {stepStatus === 'completed' && (
                <svg width="12" height="12" viewBox="0 0 12 12" fill="none">
                  <path
                    d="M10 3L4.5 8.5L2 6"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />
                </svg>
              )}
              {stepStatus === 'active' && <div className="timeline-pulse" />}
            </div>
            <div className={`timeline-label ${stepStatus}`}>{step.label}</div>
            {index < TIMELINE_STEPS.length - 1 && <div className={`timeline-line ${stepStatus}`} />}
          </div>
        );
      })}
    </div>
  );
}
