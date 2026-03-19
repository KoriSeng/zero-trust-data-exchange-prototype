import { NavLink, Outlet } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import Header from './Header';

const navClassName = ({ isActive }) =>
  `app-nav-link${isActive ? ' app-nav-link-active' : ''}`;

export default function AppShell() {
  const { profile } = useAuth();
  const roles = profile?.roles ?? [];

  const canSubmitRequests =
    roles.length === 0 || roles.includes('Requester') || roles.includes('Admin');
  const canReviewApprovals =
    roles.length === 0 || roles.includes('DataOwner') || roles.includes('Admin');

  return (
    <div className="app-shell">
      <Header />

      <nav className="app-nav" aria-label="Primary">
        <NavLink to="/" className={navClassName} end>
          Dashboard
        </NavLink>

        {canSubmitRequests && (
          <>
            <NavLink to="/datasets" className={navClassName}>
              Dataset Explorer
            </NavLink>
            <NavLink to="/requests" className={navClassName}>
              My Requests
            </NavLink>
            <NavLink to="/requests/new" className={navClassName}>
              New Request
            </NavLink>
          </>
        )}

        {canReviewApprovals && (
          <NavLink to="/approvals" className={navClassName}>
            Pending Approvals
          </NavLink>
        )}
      </nav>

      <main className="app-content">
        <Outlet />
      </main>
    </div>
  );
}
