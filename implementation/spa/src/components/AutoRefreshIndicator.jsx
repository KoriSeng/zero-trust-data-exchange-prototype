import { useEffect, useState } from 'react';

export default function AutoRefreshIndicator({ lastUpdated, isRefreshing }) {
  const [timeSince, setTimeSince] = useState('');

  useEffect(() => {
    if (!lastUpdated) return;

    function updateTime() {
      const seconds = Math.floor((Date.now() - lastUpdated) / 1000);
      if (seconds < 10) setTimeSince('just now');
      else if (seconds < 60) setTimeSince(`${seconds}s ago`);
      else setTimeSince(`${Math.floor(seconds / 60)}m ago`);
    }

    updateTime();
    const interval = setInterval(updateTime, 1000);
    return () => clearInterval(interval);
  }, [lastUpdated]);

  return (
    <div className="auto-refresh-indicator">
      {isRefreshing && (
        <svg className="refresh-spinner" width="14" height="14" viewBox="0 0 14 14">
          <circle
            cx="7"
            cy="7"
            r="5"
            stroke="currentColor"
            strokeWidth="2"
            fill="none"
            strokeLinecap="round"
            strokeDasharray="20 10"
          />
        </svg>
      )}
      <span className="refresh-text">{isRefreshing ? 'Updating...' : `Updated ${timeSince}`}</span>
    </div>
  );
}
