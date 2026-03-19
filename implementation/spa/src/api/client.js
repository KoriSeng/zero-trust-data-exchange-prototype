import { fetchAuthSession } from 'aws-amplify/auth';

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');
export const AUTH_EXPIRED_EVENT = 'ztdx:auth-expired';

export class ApiError extends Error {
  constructor(message, status) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

function toApiUrl(path) {
  if (/^https?:\/\//i.test(path)) {
    return path;
  }

  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  return API_BASE_URL ? `${API_BASE_URL}${normalizedPath}` : normalizedPath;
}

async function getAccessToken() {
  try {
    const session = await fetchAuthSession();
    return session.tokens?.accessToken?.toString() ?? null;
  } catch {
    return null;
  }
}

async function parseResponse(response) {
  if (response.status === 204) {
    return null;
  }

  const rawBody = await response.text();
  if (!rawBody) {
    return null;
  }

  try {
    return JSON.parse(rawBody);
  } catch {
    return rawBody;
  }
}

function notifyAuthExpired() {
  if (typeof window === 'undefined') {
    return;
  }

  window.setTimeout(() => {
    window.dispatchEvent(new CustomEvent(AUTH_EXPIRED_EVENT));
  }, 0);
}

export default async function apiCall(path, options = {}) {
  const headers = new Headers(options.headers ?? {});
  const token = await getAccessToken();

  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  if (!headers.has('Content-Type') && options.body && !(options.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json');
  }

  const response = await fetch(toApiUrl(path), {
    ...options,
    headers,
  });

  const payload = await parseResponse(response);

  if (!response.ok) {
    const message =
      payload && typeof payload === 'object'
        ? payload.error ?? payload.message ?? `HTTP ${response.status}`
        : payload ?? `HTTP ${response.status}`;
    const error = new ApiError(message, response.status);

    if (response.status === 401) {
      notifyAuthExpired();
    }

    throw error;
  }

  return payload;
}
