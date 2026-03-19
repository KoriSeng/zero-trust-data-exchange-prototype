import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

export default function Header() {
  const navigate = useNavigate();
  const { profile, user, signOut } = useAuth();
  const [error, setError] = useState('');
  const [showUserMenu, setShowUserMenu] = useState(false);

  const displayName = profile?.displayName ?? user?.username ?? 'Authenticated User';
  const organizationName = profile?.organizationName ?? 'Unknown organisation';

  async function handleSignOut() {
    setError('');
    try {
      await signOut();
      navigate('/login', { replace: true });
    } catch (signOutError) {
      setError(signOutError.message ?? 'Could not sign out.');
    }
  }

  return (
    <header className="modern-header">
      <div>
        <div className="header-brand">
          <div className="brand-icon">🧬</div>
          <div className="brand-text">
            <h1 className="brand-title">Zero Trust Data Exchange</h1>
            <p className="brand-subtitle">Bio Research Data Sharing Platform</p>
          </div>
        </div>

        <div className="header-right">

        <div className="user-menu-wrapper">
          <button
            type="button"
            className="user-menu-trigger"
            onClick={() => setShowUserMenu(!showUserMenu)}
            aria-expanded={showUserMenu}
            aria-haspopup="true"
          >
            <div className="user-avatar">
              {displayName.charAt(0).toUpperCase()}
            </div>
            <div className="user-info">
              <span className="user-name">{displayName}</span>
              <span className="user-org">{organizationName}</span>
            </div>
            <svg width="16" height="16" viewBox="0 0 16 16" fill="none" className="dropdown-icon">
              <path
                d="M4 6L8 10L12 6"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </button>

          {showUserMenu && (
            <div className="user-dropdown">
              <div className="dropdown-section">
                <div className="dropdown-user-details">
                  <strong>{displayName}</strong>
                  <span>{organizationName}</span>
                  <span className="user-roles">{profile?.roles?.join(', ') ?? 'No roles'}</span>
                </div>
              </div>
              <div className="dropdown-divider" />
              <button type="button" className="dropdown-item" onClick={handleSignOut}>
                <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                  <path
                    d="M6 14H3.33333C2.97971 14 2.64057 13.8595 2.39052 13.6095C2.14048 13.3594 2 13.0203 2 12.6667V3.33333C2 2.97971 2.14048 2.64057 2.39052 2.39052C2.64057 2.14048 2.97971 2 3.33333 2H6"
                    stroke="currentColor"
                    strokeWidth="1.5"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />
                  <path
                    d="M10.6667 11.3333L14 8L10.6667 4.66666"
                    stroke="currentColor"
                    strokeWidth="1.5"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />
                  <path
                    d="M14 8H6"
                    stroke="currentColor"
                    strokeWidth="1.5"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />
                </svg>
                Sign out
              </button>
            </div>
          )}
        </div>
      </div>
    </div>

    {error && <p className="error-banner">{error}</p>}
  </header>
);
}
