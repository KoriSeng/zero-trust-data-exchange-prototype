import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

export default function Header() {
  const navigate = useNavigate();
  const { profile, user, signOut } = useAuth();
  const [error, setError] = useState('');

  const displayName = profile?.displayName ?? user?.username ?? 'Authenticated User';
  const organizationName = profile?.organizationName ?? 'Unknown organisation';
  const roles = profile?.roles?.join(', ') ?? 'Role information unavailable';

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
    <header className="app-header">
      <div>
        <h1 className="app-title">Zero Trust Data Exchange</h1>
        <p className="app-subtitle">
          {displayName} · {organizationName}
        </p>
        <p className="muted-text">{roles}</p>
      </div>

      <div className="header-actions">
        <button type="button" className="secondary-button" onClick={handleSignOut}>
          Sign out
        </button>
      </div>

      {error && <p className="error-banner">{error}</p>}
    </header>
  );
}
