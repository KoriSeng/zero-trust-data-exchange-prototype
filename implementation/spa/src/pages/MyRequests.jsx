import { useCallback, useEffect, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import apiCall from '../api/client';
import RedeemRequestForm from '../components/RedeemRequestForm';
import StatusBadge from '../components/StatusBadge';
import { useAuth } from '../contexts/AuthContext';

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

  const loadRequests = useCallback(async () => {
    try {
      const response = await apiCall('/requests/my');
      setRequests(Array.isArray(response) ? response : []);
      setError('');
    } catch (loadError) {
      setError(loadError.message ?? 'Could not load requests.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (newRequestIdFromState || (Array.isArray(newRequestIdsFromState) && newRequestIdsFromState.length > 0)) {
      navigate(location.pathname, { replace: true, state: null });
    }
  }, [location.pathname, navigate, newRequestIdFromState, newRequestIdsFromState]);

  useEffect(() => {
    void loadRequests();

    const intervalId = window.setInterval(() => {
      void loadRequests();
    }, 10000);

    return () => {
      window.clearInterval(intervalId);
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
        <Link className="primary-button" to="/datasets">
          Browse datasets
        </Link>
      </div>

      {newRequestNotice && <p className="success-banner">{newRequestNotice}</p>}

      {error && <p className="error-banner">{error}</p>}

      {isLoading ? (
        <p>Loading requests…</p>
      ) : requests.length === 0 ? (
        <p>No requests submitted yet.</p>
      ) : (
        <div className="table-wrapper">
          <table>
            <thead>
              <tr>
                <th>Request ID</th>
                <th>Dataset</th>
                <th>Purpose</th>
                <th>Status</th>
                <th>Updated</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {requests.map((request) => (
                <tr key={request.id}>
                  <td>{request.requestId}</td>
                  <td>{request.datasetName ?? request.datasetId}</td>
                  <td>{request.purpose}</td>
                  <td>
                    <StatusBadge status={request.status} />
                  </td>
                  <td>{new Date(request.updatedAt ?? request.createdAt).toLocaleString()}</td>
                  <td>
                    <RedeemRequestForm request={request} onRedeemed={loadRequests} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
