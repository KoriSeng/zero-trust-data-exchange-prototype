/* eslint-disable react-refresh/only-export-components */
import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { fetchAuthSession, getCurrentUser, signOut as amplifySignOut } from 'aws-amplify/auth';
import { AUTH_EXPIRED_EVENT } from '../api/client';

const AuthContext = createContext(undefined);
const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');

async function loadCurrentProfile(idToken) {
  if (!API_BASE_URL || !idToken) {
    return null;
  }

  const response = await fetch(`${API_BASE_URL}/me`, {
    headers: {
      Authorization: `Bearer ${idToken}`,
    },
  });

  if (!response.ok) {
    throw new Error('Unable to fetch /me profile');
  }

  return response.json();
}

async function resolveSessionSnapshot() {
  try {
    const currentUser = await getCurrentUser();
    const session = await fetchAuthSession();
    const token = session.tokens?.idToken?.toString() ?? null;

    let currentProfile = null;
    if (token) {
      try {
        currentProfile = await loadCurrentProfile(token);
      } catch {
        currentProfile = null;
      }
    }

    return {
      user: currentUser,
      idToken: token,
      profile: currentProfile,
    };
  } catch {
    return {
      user: null,
      idToken: null,
      profile: null,
    };
  }
}

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [idToken, setIdToken] = useState(null);
  const [profile, setProfile] = useState(null);
  const [isLoading, setIsLoading] = useState(true);

  const clearSession = useCallback(() => {
    setUser(null);
    setIdToken(null);
    setProfile(null);
  }, []);

  const loadSession = useCallback(async () => {
    setIsLoading(true);

    try {
      const currentSession = await resolveSessionSnapshot();
      setUser(currentSession.user);
      setIdToken(currentSession.idToken);
      setProfile(currentSession.profile);
      return currentSession;
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    let isActive = true;

    async function loadSessionOnMount() {
      setIsLoading(true);

      try {
        const currentSession = await resolveSessionSnapshot();

        if (!isActive) {
          return;
        }

        setUser(currentSession.user);
        setIdToken(currentSession.idToken);
        setProfile(currentSession.profile);
      } finally {
        if (isActive) {
          setIsLoading(false);
        }
      }
    }

    void loadSessionOnMount();

    return () => {
      isActive = false;
    };
  }, []);

  useEffect(() => {
    if (typeof window === 'undefined') {
      return undefined;
    }

    function handleAuthExpired() {
      clearSession();
      setIsLoading(false);
    }

    window.addEventListener(AUTH_EXPIRED_EVENT, handleAuthExpired);
    return () => {
      window.removeEventListener(AUTH_EXPIRED_EVENT, handleAuthExpired);
    };
  }, [clearSession]);

  const getIdToken = useCallback(async () => {
    try {
      const session = await fetchAuthSession();
      const token = session.tokens?.idToken?.toString() ?? null;
      setIdToken(token);
      return token;
    } catch {
      setIdToken(null);
      return null;
    }
  }, []);

  const refreshProfile = useCallback(async () => {
    const token = await getIdToken();
    if (!token) {
      setProfile(null);
      return null;
    }

    try {
      const latestProfile = await loadCurrentProfile(token);
      setProfile(latestProfile);
      return latestProfile;
    } catch {
      setProfile(null);
      return null;
    }
  }, [getIdToken]);

  const signOut = useCallback(async () => {
    await amplifySignOut();
    clearSession();
  }, [clearSession]);

  const value = useMemo(
    () => ({
      user,
      idToken,
      profile,
      isLoading,
      getIdToken,
      signOut,
      reload: loadSession,
      refreshProfile,
    }),
    [idToken, isLoading, getIdToken, loadSession, profile, refreshProfile, signOut, user],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }

  return context;
}
