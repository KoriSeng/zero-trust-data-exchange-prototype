import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import Header from './Header';

const navClassName = ({ isActive }) =>
  `modern-nav-link${isActive ? ' modern-nav-link-active' : ''}`;

export default function AppShell() {
  const navigate = useNavigate();
  const { profile } = useAuth();
  const roles = profile?.roles ?? [];

  const canSubmitRequests =
    roles.length === 0 || roles.includes('Requester') || roles.includes('Admin');
  const canReviewApprovals =
    roles.length === 0 || roles.includes('DataOwner') || roles.includes('Admin');

  return (
    <div className="app-shell">
      <Header />

      <nav className="modern-nav-container" aria-label="Primary">
        <div className="modern-nav-tabs">
          <NavLink to="/" className={navClassName} end>
            <svg width="18" height="18" viewBox="0 0 18 18" fill="none">
              <path
                d="M2.25 6.75L9 1.5L15.75 6.75V15C15.75 15.3978 15.592 15.7794 15.3107 16.0607C15.0294 16.342 14.6478 16.5 14.25 16.5H3.75C3.35218 16.5 2.97064 16.342 2.68934 16.0607C2.40804 15.7794 2.25 15.3978 2.25 15V6.75Z"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
            Dashboard
          </NavLink>

          {canSubmitRequests && (
            <>
              <NavLink to="/datasets" className={navClassName}>
                <svg width="18" height="18" viewBox="0 0 18 18" fill="none">
                  <path
                    d="M15.75 2.25H2.25C1.42157 2.25 0.75 2.92157 0.75 3.75V14.25C0.75 15.0784 1.42157 15.75 2.25 15.75H15.75C16.5784 15.75 17.25 15.0784 17.25 14.25V3.75C17.25 2.92157 16.5784 2.25 15.75 2.25Z"
                    stroke="currentColor"
                    strokeWidth="1.5"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />
                  <path d="M0.75 7.5H17.25" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
                </svg>
                Data
              </NavLink>
              <NavLink to="/requests" className={navClassName}>
                <svg width="18" height="18" viewBox="0 0 18 18" fill="none">
                  <path
                    d="M14.25 2.25H3.75C2.92157 2.25 2.25 2.92157 2.25 3.75V14.25C2.25 15.0784 2.92157 15.75 3.75 15.75H14.25C15.0784 15.75 15.75 15.0784 15.75 14.25V3.75C15.75 2.92157 15.0784 2.25 14.25 2.25Z"
                    stroke="currentColor"
                    strokeWidth="1.5"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />
                  <path d="M12 9L8.25 12.75L6 10.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
                Requests
              </NavLink>
            </>
          )}

          {canReviewApprovals && (
            <NavLink to="/approvals" className={navClassName}>
              <svg width="18" height="18" viewBox="0 0 18 18" fill="none">
                <path
                  d="M16.5 8.25V9C16.4991 10.6106 15.9754 12.1783 15.007 13.4733C14.0386 14.7683 12.6775 15.7238 11.1265 16.201C9.57557 16.6781 7.91794 16.6523 6.38217 16.1275C4.8464 15.6027 3.51552 14.6056 2.58665 13.2802C1.65779 11.9547 1.17939 10.3678 1.21803 8.75833C1.25667 7.14883 1.81024 5.58827 2.80137 4.30739C3.7925 3.02651 5.17174 2.08904 6.73913 1.63552C8.30651 1.18199 9.97937 1.23406 11.5163 1.785"
                  stroke="currentColor"
                  strokeWidth="1.5"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
                <path d="M16.5 3L9 10.5075L6.75 8.2575" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
              Approvals
            </NavLink>
          )}
        </div>
      </nav>

      <div className="quick-actions-bar-wrapper">
        <div className="quick-actions-bar">
          <span className="quick-actions-label">Quick Actions:</span>
          {canSubmitRequests && (
            <>
              <button type="button" className="quick-action-button" onClick={() => navigate('/datasets')}>
                <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                  <path
                    d="M14 2H2C1.44772 2 1 2.44772 1 3V13C1 13.5523 1.44772 14 2 14H14C14.5523 14 15 13.5523 15 13V3C15 2.44772 14.5523 2 14 2Z"
                    stroke="currentColor"
                    strokeWidth="1.5"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />
                  <path d="M1 6H15" stroke="currentColor" strokeWidth="1.5" />
                </svg>
                Browse Datasets
              </button>
              <button type="button" className="quick-action-button" onClick={() => navigate('/datasets')}>
                <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                  <path d="M8 3.33334V12.6667" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                  <path d="M3.33334 8H12.6667" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
                New Request
              </button>
            </>
          )}
          {canReviewApprovals && (
            <button type="button" className="quick-action-button" onClick={() => navigate('/approvals')}>
              <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                <path
                  d="M14.6667 7.38667V8C14.666 9.43767 14.2004 10.8365 13.3395 11.9879C12.4786 13.1393 11.269 13.9817 9.89028 14.3893C8.51154 14.797 7.03794 14.7479 5.68668 14.2497C4.33542 13.7516 3.18139 12.8308 2.38574 11.6247C1.59008 10.4186 1.1929 8.99113 1.22936 7.53629C1.26582 6.08145 1.73399 4.67637 2.56675 3.51283C3.39951 2.34929 4.5541 1.48417 5.87987 1.03726C7.20564 0.590352 8.63955 0.586502 9.96761 1.026"
                  stroke="currentColor"
                  strokeWidth="1.5"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
                <path d="M14.6667 2.66667L8 9.34L6 7.34" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
              Review Queue
            </button>
          )}
        </div>
      </div>

      <main className="app-content">
        <Outlet />
      </main>
    </div>
  );
}
