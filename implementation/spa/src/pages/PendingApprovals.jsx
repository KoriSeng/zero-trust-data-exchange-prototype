import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import ApprovalPanel from '../components/ApprovalPanel';
import RejectModal from '../components/RejectModal';
import StatusBadge from '../components/StatusBadge';
import apiCall from '../api/client';
import { useAuth } from '../contexts/AuthContext';

const TAB_PENDING = 'pending';
const TAB_APPROVED = 'approved';

export default function PendingApprovals() {
  const { profile } = useAuth();

  const roles = profile?.roles ?? [];
  const canReviewApprovals =
    roles.length === 0 || roles.includes('DataOwner') || roles.includes('Admin');

  const [activeTab, setActiveTab] = useState(TAB_PENDING);
  const [pendingRequests, setPendingRequests] = useState([]);
  const [approvedRequests, setApprovedRequests] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [expandedRequestId, setExpandedRequestId] = useState(null);
  const [rejectingRequestId, setRejectingRequestId] = useState(null);
  const [otpNotice, setOtpNotice] = useState(null);

  const loadRequests = useCallback(async () => {
    try {
      const [pendingResponse, approvedResponse] = await Promise.all([
        apiCall('/requests/pending'),
        apiCall('/requests/owner-review'),
      ]);
      setPendingRequests(Array.isArray(pendingResponse) ? pendingResponse : []);
      setApprovedRequests(Array.isArray(approvedResponse) ? approvedResponse : []);
      setError('');
    } catch (loadError) {
      setError(loadError.message ?? 'Could not load approval queues.');
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

  const activeRequests = activeTab === TAB_PENDING ? pendingRequests : approvedRequests;

  const rejectingRequest = useMemo(
    () =>
      [...pendingRequests, ...approvedRequests].find(
        (request) => request.id === rejectingRequestId,
      ) ?? null,
    [rejectingRequestId, pendingRequests, approvedRequests],
  );

  function removeRequest(requestId) {
    setPendingRequests((current) => current.filter((request) => request.id !== requestId));
    setApprovedRequests((current) => current.filter((request) => request.id !== requestId));
    setExpandedRequestId(null);
    setRejectingRequestId(null);
  }

  function handleApproved(request, result) {
    setPendingRequests((current) => current.filter((item) => item.id !== request.id));
    setApprovedRequests((current) => [
      { ...request, status: result?.status ?? 'OtpSent', updatedAt: new Date().toISOString() },
      ...current.filter((item) => item.id !== request.id),
    ]);
    setExpandedRequestId(null);
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
      <h2>Approvals</h2>

      <div className="button-row">
        <button
          type="button"
          className={activeTab === TAB_PENDING ? 'primary-button' : 'secondary-button'}
          onClick={() => setActiveTab(TAB_PENDING)}
        >
          Pending ({pendingRequests.length})
        </button>
        <button
          type="button"
          className={activeTab === TAB_APPROVED ? 'primary-button' : 'secondary-button'}
          onClick={() => setActiveTab(TAB_APPROVED)}
        >
          Approved / Active ({approvedRequests.length})
        </button>
      </div>

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
        <p>Loading requests…</p>
      ) : activeRequests.length === 0 ? (
        <p>
          {activeTab === TAB_PENDING
            ? 'No pending requests for your organisation.'
            : 'No approved/active requests available.'}
        </p>
      ) : (
        <div className="stack-gap">
          {activeRequests.map((request) => (
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
                  {activeTab === TAB_PENDING && (
                    <button
                      type="button"
                      className="secondary-button"
                      onClick={() =>
                        setExpandedRequestId(expandedRequestId === request.id ? null : request.id)
                      }
                    >
                      {expandedRequestId === request.id ? 'Hide review' : 'Review'}
                    </button>
                  )}
                  <button
                    type="button"
                    className="danger-button"
                    onClick={() => setRejectingRequestId(request.id)}
                  >
                    {request.status === 'Redeemed' ? 'Revoke now' : 'Reject'}
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

              {activeTab === TAB_PENDING && expandedRequestId === request.id && (
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
