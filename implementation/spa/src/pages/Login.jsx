import { useEffect, useState } from 'react';
import { signInWithRedirect } from 'aws-amplify/auth';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

export default function Login() {
  const { user, isLoading } = useAuth();
  const [error, setError] = useState('');

  useEffect(() => {
    if (isLoading) return;

    if (user) {
      // Already authenticated, will be handled by Navigate below
      return;
    }

    // Redirect unauthenticated users directly to Cognito
    async function redirectToCognito() {
      try {
        // signInWithRedirect without provider redirects to Cognito Hosted UI
        // where users can choose from configured IDPs (IDP-A, IDP-B)
        await signInWithRedirect();
      } catch (loginError) {
        setError(loginError.message ?? 'Could not start sign-in flow.');
      }
    }

    void redirectToCognito();
  }, [user, isLoading]);

  if (isLoading) {
    return (
      <div className="page-shell center-text">
        <p>Loading session…</p>
      </div>
    );
  }

  if (user) {
    return <Navigate to="/" replace />;
  }

  // Show error message if redirect failed, otherwise show loading state
  if (error) {
    return (
      <div className="auth-page">
        <section className="card">
          <h1>Sign-in Error</h1>
          <p className="error-banner">{error}</p>
          <button
            type="button"
            className="primary-button"
            onClick={() => window.location.reload()}
          >
            Try Again
          </button>
        </section>
      </div>
    );
  }

  return (
    <div className="page-shell center-text">
      <p>Redirecting to sign-in…</p>
    </div>
  );
}
