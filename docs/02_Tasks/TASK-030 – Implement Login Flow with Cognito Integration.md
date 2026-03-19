---
type: Task
task_id: TASK-030
title: "Implement Login Flow with Cognito Integration"
owner: STK-001
status: "In Progress"
related_milestone: MS-009
---

## Description

Implement the complete authentication flow for the SPA: two IdP-specific sign-in buttons on a Login page, a Callback page that exchanges the authorization code for tokens via Amplify, a React Context that exposes auth state application-wide, a reusable API client that attaches the `id_token` as a Bearer header, and a `<RequireAuth>` guard component that protects authenticated routes.

## Implementation Notes

### 1. Login page — `src/pages/Login.jsx`

Show two distinct sign-in buttons, one per federated IdP. Amplify's `signInWithRedirect` sends the browser to the Cognito Hosted UI, which then redirects to the correct IdP.

```jsx
import { signInWithRedirect } from 'aws-amplify/auth';

export default function Login() {
  return (
    <div className="login-container">
      <h1>Zero Trust Data Exchange</h1>
      <p>Sign in to continue</p>
      <button onClick={() => signInWithRedirect({ provider: { custom: 'IDP-A' } })}>
        Sign in as Requester (IDP-A)
      </button>
      <button onClick={() => signInWithRedirect({ provider: { custom: 'IDP-B' } })}>
        Sign in as Data Owner (IDP-B)
      </button>
    </div>
  );
}
```

> The string `'IDP-A'` / `'IDP-B'` must match the **Provider name** configured in the Cognito User Pool (set in TASK-022). Confirm the exact casing with `terraform output` or the AWS Console.

### 2. Callback page — `src/pages/Callback.jsx`

After Cognito redirects back to `/callback`, Amplify automatically completes the PKCE token exchange. The Callback component just needs to wait for that to finish and then navigate to the dashboard.

```jsx
import { useEffect } from 'react';
import { getCurrentUser } from 'aws-amplify/auth';
import { useNavigate } from 'react-router-dom';

export default function Callback() {
  const navigate = useNavigate();

  useEffect(() => {
    // Amplify processes the ?code= query param automatically on mount.
    // Poll until getCurrentUser() resolves, then redirect.
    getCurrentUser()
      .then(() => navigate('/', { replace: true }))
      .catch(() => navigate('/login', { replace: true }));
  }, [navigate]);

  return <p>Completing sign-in…</p>;
}
```

### 3. Auth context — `src/contexts/AuthContext.jsx`

A single React Context makes `user`, `idToken`, and auth actions available anywhere in the tree without prop drilling.

```jsx
import { createContext, useContext, useEffect, useState } from 'react';
import {
  getCurrentUser,
  fetchAuthSession,
  signOut as amplifySignOut
} from 'aws-amplify/auth';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser]         = useState(null);
  const [idToken, setIdToken]   = useState(null);
  const [isLoading, setLoading] = useState(true);

  useEffect(() => {
    loadSession();
  }, []);

  async function loadSession() {
    try {
      const currentUser = await getCurrentUser();
      const session     = await fetchAuthSession();
      setUser(currentUser);
      setIdToken(session.tokens?.idToken?.toString() ?? null);
    } catch {
      setUser(null);
      setIdToken(null);
    } finally {
      setLoading(false);
    }
  }

  async function getIdToken() {
    // Always fetch a fresh session — Amplify refreshes the token automatically
    const session = await fetchAuthSession();
    return session.tokens?.idToken?.toString() ?? null;
  }

  async function signOut() {
    await amplifySignOut();
    setUser(null);
    setIdToken(null);
  }

  return (
    <AuthContext.Provider value={{ user, idToken, isLoading, getIdToken, signOut, reload: loadSession }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  return useContext(AuthContext);
}
```

Wrap `<App />` (or the router) with `<AuthProvider>` in `src/main.jsx`.

### 4. Protected route — `src/components/RequireAuth.jsx`

```jsx
import { Navigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

export default function RequireAuth({ children }) {
  const { user, isLoading } = useAuth();
  if (isLoading) return <p>Loading…</p>;
  if (!user)     return <Navigate to="/login" replace />;
  return children;
}
```

Use it in `App.jsx`:

```jsx
<Route path="/*" element={<RequireAuth><Dashboard /></RequireAuth>} />
```

### 5. API client — `src/api/client.js`

One thin wrapper around `fetch` that injects the current `id_token`. All API modules import this.

```js
import { fetchAuthSession } from 'aws-amplify/auth';

const BASE = import.meta.env.VITE_API_BASE_URL;

export default async function apiCall(path, options = {}) {
  const session = await fetchAuthSession();
  const token   = session.tokens?.idToken?.toString();

  const response = await fetch(`${BASE}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
      ...options.headers
    }
  });

  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.message ?? `HTTP ${response.status}`);
  }

  return response.json();
}
```

Usage example:

```js
import apiCall from '../api/client';
const me = await apiCall('/me');
```

### 6. Display current user in the header

After a successful login, call `GET /me` to get the user's `displayName`, `organizationName`, and `roles`. Store the result in the auth context (or a small `useState` in the header component) and render it in the app shell:

```jsx
// src/components/Header.jsx
import { useEffect, useState } from 'react';
import apiCall from '../api/client';
import { useAuth } from '../contexts/AuthContext';

export default function Header() {
  const { signOut } = useAuth();
  const [me, setMe] = useState(null);

  useEffect(() => {
    apiCall('/me').then(setMe).catch(() => {});
  }, []);

  return (
    <header>
      <span>Zero Trust Data Exchange</span>
      {me && <span>{me.displayName} · {me.organizationName}</span>}
      <button onClick={signOut}>Sign out</button>
    </header>
  );
}
```

The `identityProvider` field from `/me` also tells you whether the user is an IDP-A (Requester) or IDP-B (DataOwner), which drives role-based navigation in the Dashboard (TASK-031 / TASK-032).

### 7. Token storage

Amplify v6 stores tokens in `localStorage` by default under keys prefixed with `CognitoIdentityServiceProvider`. No manual storage handling is needed. On `signOut()`, Amplify clears the tokens automatically.

### 8. Session expiry handling

For the POC, it is sufficient to catch `NotAuthorizedException` / `No current user` errors from `fetchAuthSession()` in `apiCall` and redirect to `/login`:

```js
// In apiCall, replace the throw with:
if (response.status === 401) {
  window.location.href = '/login';
  return;
}
```

## Definition of Done

- [ ] Login page renders with two IdP-specific sign-in buttons
- [ ] Clicking either button redirects to the Cognito Hosted UI
- [ ] After Hosted UI redirect, `/callback` route completes the token exchange
- [ ] `AuthContext` exposes `user`, `idToken`, `isLoading`, `signOut`, `getIdToken`
- [ ] `RequireAuth` redirects unauthenticated users to `/login`
- [ ] `apiCall('/me')` returns current user data and it renders in the header
- [ ] `signOut()` clears the session and returns to the Login page
- [ ] All API calls attach `Authorization: Bearer <id_token>` header
