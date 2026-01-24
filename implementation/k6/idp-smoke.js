import http from 'k6/http';
import { check, group } from 'k6';

export const options = {
  vus: 1,
  iterations: 1
};

const IDP_A_BASE = __ENV.IDP_A_BASE_URL || 'http://localhost:9001';
const IDP_B_BASE = __ENV.IDP_B_BASE_URL || 'http://localhost:9002';

const CLIENT_ID = __ENV.OIDC_CLIENT_ID || 'k6-client';
const CLIENT_SECRET = __ENV.OIDC_CLIENT_SECRET || 'k6-secret';

const USERS = {
  A: { username: __ENV.IDP_A_USER || 'a.alex', password: __ENV.IDP_A_PASS || 'pass-a-alex' },
  B: { username: __ENV.IDP_B_USER || 'b.alex', password: __ENV.IDP_B_PASS || 'pass-b-alex' }
};

function authCodeFlow(baseUrl, user) {
  const disco = http.get(`${baseUrl}/.well-known/openid-configuration`, { redirects: 0 });
  check(disco, { 'discovery 200': (r) => r.status === 200 });

  const jwks = http.get(`${baseUrl}/jwks`, { redirects: 0 });
  check(jwks, { 'jwks 200': (r) => r.status === 200 });

  const redirectUri = `${baseUrl}/k6-callback`;
  const authorizeUrl = `${baseUrl}/authorize?response_type=code&client_id=${encodeURIComponent(CLIENT_ID)}&redirect_uri=${encodeURIComponent(redirectUri)}&scope=openid&state=k6`;

  const auth = http.get(authorizeUrl, { redirects: 0 });
  check(auth, {
    'authorize returns login page': (r) => r.status === 200 && (r.headers['Content-Type'] || '').includes('text/html')
  });

  const login = http.post(`${baseUrl}/login`, {
    username: user.username,
    password: user.password,
    client_id: CLIENT_ID,
    redirect_uri: redirectUri,
    scope: 'openid',
    response_type: 'code',
    state: 'k6'
  }, {
    redirects: 0,
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' }
  });

  check(login, {
    'login redirects': (r) => r.status === 302,
    'login has Location': (r) => !!r.headers.Location
  });

  const location = login.headers.Location;
  const codeMatch = location && location.match(/[\?&]code=([^&]+)/);
  check(codeMatch, { 'redirect contains code': (m) => !!m });

  const code = codeMatch ? codeMatch[1] : null;
  if (!code) return;

  const token = http.post(`${baseUrl}/token`, {
    grant_type: 'authorization_code',
    code,
    redirect_uri: redirectUri,
    client_id: CLIENT_ID,
    client_secret: CLIENT_SECRET
  }, {
    redirects: 0,
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' }
  });

  check(token, {
    'token 200': (r) => r.status === 200,
    'token has id_token': (r) => !!r.json('id_token'),
    'token has access_token': (r) => !!r.json('access_token')
  });
}

export default function () {
  group('IdP-A', () => authCodeFlow(IDP_A_BASE, USERS.A));
  group('IdP-B', () => authCodeFlow(IDP_B_BASE, USERS.B));
}
