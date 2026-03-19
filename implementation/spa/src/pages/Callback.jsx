import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

const MAX_ATTEMPTS = 8;
const RETRY_DELAY_MS = 600;

export default function Callback() {
  const navigate = useNavigate();
  const { reload } = useAuth();
  const [message, setMessage] = useState('Completing sign-in…');

  useEffect(() => {
    let cancelled = false;

    async function finalizeSignIn() {
      for (let attempt = 0; attempt < MAX_ATTEMPTS; attempt += 1) {
        const session = await reload();
        if (cancelled) {
          return;
        }

        if (session.user) {
          navigate('/', { replace: true });
          return;
        }

        await new Promise((resolve) => {
          window.setTimeout(resolve, RETRY_DELAY_MS);
        });
      }

      setMessage('Sign-in could not be completed. Redirecting to login…');
      window.setTimeout(() => {
        if (!cancelled) {
          navigate('/login', { replace: true });
        }
      }, 1500);
    }

    void finalizeSignIn();

    return () => {
      cancelled = true;
    };
  }, [navigate, reload]);

  return (
    <div className="page-shell center-text">
      <p>{message}</p>
    </div>
  );
}
