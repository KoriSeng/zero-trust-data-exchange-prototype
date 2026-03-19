import { Link } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

export default function Dashboard() {
  const { profile, user, isLoading } = useAuth();

  if (isLoading) {
    return (
      <section className="content-page">
        <p>Loading dashboard…</p>
      </section>
    );
  }

  const roles = profile?.roles ?? [];
  const canSubmitRequests =
    roles.length === 0 || roles.includes('Requester') || roles.includes('Admin');
  const canReviewApprovals =
    roles.length === 0 || roles.includes('DataOwner') || roles.includes('Admin');

  return (
    <section className="content-page stack-gap">
      <div className="card">
        <h2>Welcome</h2>
        <p>
          Signed in as <strong>{profile?.displayName ?? user?.username ?? 'Authenticated User'}</strong>.
        </p>
        <p>
          Organisation: <strong>{profile?.organizationName ?? 'Unknown'}</strong>
        </p>
        <p>
          Identity provider: <strong>{profile?.identityProvider ?? 'Unknown'}</strong>
        </p>
      </div>

      <div className="grid-two">
        {canSubmitRequests && (
          <article className="card">
            <h3>Requester workflow</h3>
            <p>Create requests, monitor status changes, and redeem OTP codes for data access.</p>
            <div className="button-row">
              <Link to="/datasets" className="text-link">
                Browse datasets
              </Link>
              <Link to="/requests" className="text-link">
                View my requests
              </Link>
            </div>
          </article>
        )}

        {canReviewApprovals && (
          <article className="card">
            <h3>Data owner workflow</h3>
            <p>Review incoming requests, approve to issue OTP codes, or deny with reason.</p>
            <div className="button-row">
              <Link to="/approvals" className="text-link">
                Open pending approvals
              </Link>
            </div>
          </article>
        )}

        {!canSubmitRequests && !canReviewApprovals && (
          <article className="card">
            <h3>No workflow assigned</h3>
            <p>
              This account is authenticated but has no requester/data-owner role in the backend profile.
            </p>
          </article>
        )}
      </div>
    </section>
  );
}
