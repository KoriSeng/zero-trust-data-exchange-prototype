import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import ApprovalPanel from '../components/ApprovalPanel';
import RejectModal from '../components/RejectModal';
import StatusBadge from '../components/StatusBadge';
import apiCall from '../api/client';
import { useAuth } from '../contexts/AuthContext';

export default function PendingApprovals() {
  const { profile } = useAuth();

  const roles = profile?.roles ?? [];
  const canReviewApprovals =
    roles.length === 0 || roles.includes('DataOwner') || roles.includes('Admin');

  const [requests, setRequests] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [expandedRequestId, setExpandedRequestId] = useState(null);
  const [rejectingRequestId, setRejectingRequestId] = useState(null);
  const [otpNotice, setOtpNotice] = useState(null);

  const loadRequests = useCallback(async () => {
    try {
      const response = await apiCall('/requests/pending');
      setRequests(Array.isArray(response) ? response : []);
      setError('');
    } catch (loadError) {
      setError(loadError.message ?? 'Could not load pending approvals.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadRequests();

    const intervalId = window.setInterval(() => {
      void loadRequests();
    }, 15000);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [loadRequests]);

  const rejectingRequest = useMemo(
    () => requests.find((request) => request.id === rejectingRequestId) ?? null,
    [rejectingRequestId, requests],
  );

  function removeRequest(requestId) {
    setRequests((current) => current.filter((request) => request.id !== requestId));
    setExpandedRequestId(null);
    setRejectingRequestId(null);
  }

  function handleApproved(request, result) {
    removeRequest(request.id);
    setOtpNotice({
      requestId: request.requestId,
      message: result?.message ?? 'Request approved.',
    });
  }

  if (!canReviewApprovals) {
    return (
      <section className="content-page">
        <p className="error-banner">Access denied: data owner role is required.</p>
      </section>
    );
  }

  return (
    <section className="content-page stack-gap">
      <h2>Pending approvals</h2>

      {otpNotice && (
        <div className="success-banner">
          <p>
            Request <strong>{otpNotice.requestId}</strong> approved.
          </p>
          <p>{otpNotice.message}</p>
        </div>
      )}

      {error && <p className="error-banner">{error}</p>}

      {isLoading ? (
        <p>Loading pending approvals…</p>
      ) : requests.length === 0 ? (
        <p>No pending requests for your organisation.</p>
      ) : (
        <div className="stack-gap">
          {requests.map((request) => (
            <article className="card" key={request.id}>
              <div className="section-header">
                <div>
                  <h3>{request.datasetName ?? request.datasetId}</h3>
                  <p className="muted-text">
                    {request.requestId} · {request.requesterEmail}
                  </p>
                </div>

                <div className="button-row">
                  <StatusBadge status={request.status} />
                  <button
                    type="button"
                    className="secondary-button"
                    onClick={() =>
                      setExpandedRequestId(expandedRequestId === request.id ? null : request.id)
                    }
                  >
                    {expandedRequestId === request.id ? 'Hide review' : 'Review'}
                  </button>
                  <button
                    type="button"
                    className="danger-button"
                    onClick={() => setRejectingRequestId(request.id)}
                  >
                    Reject
                  </button>
                  <Link
                    className="text-link"
                    to={`/requests/${request.id}/audit`}
                    state={{ from: '/approvals' }}
                  >
                    Audit logs
                  </Link>
                </div>
              </div>

              <p className="muted-text">
                Submitted {new Date(request.createdAt).toLocaleString()} · Purpose: {request.purpose}
              </p>

              {expandedRequestId === request.id && (
                <div className="mt-4">
                  <ApprovalPanel request={request} onApproved={(result) => handleApproved(request, result)} />
                </div>
              )}
            </article>
          ))}
        </div>
      )}

      {rejectingRequest && (
        <RejectModal
          requestId={rejectingRequest.id}
          onRejected={() => removeRequest(rejectingRequest.id)}
          onCancel={() => setRejectingRequestId(null)}
        />
      )}
    </section>
  );
}
