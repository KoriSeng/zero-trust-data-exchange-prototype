import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import apiCall from '../api/client';
import RedeemRequestForm from '../components/RedeemRequestForm';
import StatusBadge from '../components/StatusBadge';
import StatusTimeline from '../components/StatusTimeline';
import AutoRefreshIndicator from '../components/AutoRefreshIndicator';
import { useAuth } from '../contexts/AuthContext';

const POLLING_INTERVAL = 25000; // 25 seconds

export default function MyRequests() {
  const location = useLocation();
  const navigate = useNavigate();
  const { profile } = useAuth();

  const roles = profile?.roles ?? [];
  const canViewRequests =
    roles.length === 0 || roles.includes('Requester') || roles.includes('Admin');

  const newRequestIdFromState = location.state?.newRequestId;
  const newRequestIdsFromState = location.state?.newRequestIds;
  const [newRequestNotice] = useState(() => {
    if (Array.isArray(newRequestIdsFromState) && newRequestIdsFromState.length > 0) {
      return `${newRequestIdsFromState.length} request(s) submitted: ${newRequestIdsFromState.join(', ')}`;
    }

    if (newRequestIdFromState) {
      return `Request ${newRequestIdFromState} submitted successfully.`;
    }

    return null;
  });

  const [requests, setRequests] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [lastUpdated, setLastUpdated] = useState(null);
  const [expandedRequestId, setExpandedRequestId] = useState(null);
  const pollingIntervalRef = useRef(null);
  const isPageVisibleRef = useRef(true);

  const loadRequests = useCallback(async (isManualRefresh = false) => {
    if (isManualRefresh) {
      setIsRefreshing(true);
    }
    try {
      const response = await apiCall('/requests/my');
      setRequests(Array.isArray(response) ? response : []);
      setError('');
      setLastUpdated(Date.now());
    } catch (loadError) {
      setError(loadError.message ?? 'Could not load requests.');
    } finally {
      setIsLoading(false);
      if (isManualRefresh) {
        setIsRefreshing(false);
      }
    }
  }, []);

  useEffect(() => {
    if (newRequestIdFromState || (Array.isArray(newRequestIdsFromState) && newRequestIdsFromState.length > 0)) {
      navigate(location.pathname, { replace: true, state: null });
    }
  }, [location.pathname, navigate, newRequestIdFromState, newRequestIdsFromState]);

  // Initial load
  useEffect(() => {
    void loadRequests();
  }, [loadRequests]);

  // Smart polling with page visibility
  useEffect(() => {
    function handleVisibilityChange() {
      isPageVisibleRef.current = !document.hidden;
      if (isPageVisibleRef.current) {
        void loadRequests();
      }
    }

    document.addEventListener('visibilitychange', handleVisibilityChange);

    pollingIntervalRef.current = setInterval(() => {
      if (isPageVisibleRef.current) {
        void loadRequests();
      }
    }, POLLING_INTERVAL);

    return () => {
      document.removeEventListener('visibilitychange', handleVisibilityChange);
      if (pollingIntervalRef.current) {
        clearInterval(pollingIntervalRef.current);
      }
    };
  }, [loadRequests]);

  if (!canViewRequests) {
    return (
      <section className="content-page">
        <p className="error-banner">Access denied: requester role is required.</p>
      </section>
    );
  }

  return (
    <section className="content-page stack-gap">
      <div className="section-header">
        <h2>My Requests</h2>
        <div className="button-row">
          <AutoRefreshIndicator lastUpdated={lastUpdated} isRefreshing={isRefreshing} />
          <button type="button" className="secondary-button" onClick={() => void loadRequests(true)} disabled={isRefreshing}>
            {isRefreshing ? 'Refreshing…' : 'Refresh'}
          </button>
          <Link className="primary-button" to="/datasets">
            Browse datasets
          </Link>
        </div>
      </div>

      {newRequestNotice && <p className="success-banner">{newRequestNotice}</p>}

      {error && <p className="error-banner">{error}</p>}

      {isLoading ? (
        <div className="request-cards-grid">
          {[1, 2, 3].map((i) => (
            <div key={i} className="request-card skeleton">
              <div className="skeleton-line skeleton-title" />
              <div className="skeleton-line skeleton-text" />
              <div className="skeleton-line skeleton-text" />
            </div>
          ))}
        </div>
      ) : requests.length === 0 ? (
        <div className="empty-state">
          <div className="empty-state-icon">📋</div>
          <h3>No requests yet</h3>
          <p>Submit your first data access request to get started.</p>
          <Link className="primary-button" to="/datasets">
            Browse datasets
          </Link>
        </div>
      ) : (
        <div className="request-cards-grid">
          {requests.map((request) => (
            <article key={request.id} className="request-card">
              <div className="request-card-header">
                <div>
                  <h3 className="request-card-title">{request.requestId}</h3>
                  <p className="request-card-dataset">{request.datasetName ?? request.datasetId}</p>
                </div>
                <StatusBadge status={request.status} />
              </div>

              <StatusTimeline status={request.status} />

              <div className="request-card-meta">
                <div className="request-meta-item">
                  <span className="meta-label">Purpose</span>
                  <p className="meta-value">{request.purpose}</p>
                </div>
                <div className="request-meta-item">
                  <span className="meta-label">Updated</span>
                  <p className="meta-value">{new Date(request.updatedAt ?? request.createdAt).toLocaleString()}</p>
                </div>
                {request.objectKeys && request.objectKeys.length > 0 && (
                  <div className="request-meta-item">
                    <span className="meta-label">Files</span>
                    <p className="meta-value">{request.objectKeys.length} file(s)</p>
                  </div>
                )}
              </div>

              <div className="request-card-actions">
                <button
                  type="button"
                  className="card-expand-button"
                  onClick={() => setExpandedRequestId(expandedRequestId === request.id ? null : request.id)}
                >
                  {expandedRequestId === request.id ? 'Hide details' : 'Show details'}
                </button>
              </div>

              {expandedRequestId === request.id && (
                <div className="request-card-expanded">
                  <RedeemRequestForm request={request} onRedeemed={loadRequests} />
                </div>
              )}
            </article>
          ))}
        </div>
      )}
    </section>
  );
}
