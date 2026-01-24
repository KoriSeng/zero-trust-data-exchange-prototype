import http from 'node:http';
import { URL, URLSearchParams } from 'node:url';

function request(method, url, { headers, body } = {}) {
  return new Promise((resolve, reject) => {
    const u = new URL(url);
    const req = http.request({
      method,
      hostname: u.hostname,
      port: u.port,
      path: u.pathname + u.search,
      headers
    }, (res) => {
      const chunks = [];
      res.on('data', (c) => chunks.push(c));
      res.on('end', () => {
        const text = Buffer.concat(chunks).toString('utf8');
        resolve({ status: res.statusCode, headers: res.headers, text });
      });
    });
    req.on('error', reject);
    if (body) req.write(body);
    req.end();
  });
}

async function run() {
  const base = process.env.BASE_URL || 'http://localhost:9001';
  const clientId = process.env.OIDC_CLIENT_ID || 'smoke-client';
  const clientSecret = process.env.OIDC_CLIENT_SECRET || 'smoke-secret';
  const username = process.env.IDP_USER || 'a.alex';
  const password = process.env.IDP_PASS || 'pass-a-alex';

  const disco = await request('GET', `${base}/.well-known/openid-configuration`);
  if (disco.status !== 200) throw new Error(`discovery failed: ${disco.status}`);

  const jwks = await request('GET', `${base}/jwks`);
  if (jwks.status !== 200) throw new Error(`jwks failed: ${jwks.status}`);

  const redirectUri = `${base}/smoke-callback`;
  const authUrl = `${base}/authorize?response_type=code&client_id=${encodeURIComponent(clientId)}&redirect_uri=${encodeURIComponent(redirectUri)}&scope=openid&state=smoke`;
  const auth = await request('GET', authUrl);
  if (auth.status !== 200) throw new Error(`authorize did not return login page: ${auth.status}`);

  const form = new URLSearchParams({
    username,
    password,
    client_id: clientId,
    redirect_uri: redirectUri,
    scope: 'openid',
    response_type: 'code',
    state: 'smoke'
  });

  const login = await request('POST', `${base}/login`, {
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: form.toString()
  });

  if (login.status !== 302) throw new Error(`login failed: ${login.status}`);
  const location = login.headers.location;
  if (!location) throw new Error('login missing Location header');

  const match = location.match(/[?&]code=([^&]+)/);
  if (!match) throw new Error('no code in redirect');
  const code = match[1];

  const tokenForm = new URLSearchParams({
    grant_type: 'authorization_code',
    code,
    redirect_uri: redirectUri,
    client_id: clientId,
    client_secret: clientSecret
  });

  const token = await request('POST', `${base}/token`, {
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: tokenForm.toString()
  });

  if (token.status !== 200) throw new Error(`token failed: ${token.status} ${token.text}`);
  const json = JSON.parse(token.text);
  if (!json.id_token || !json.access_token) throw new Error('token response missing id_token/access_token');

  console.log('SMOKE PASS', { base });
}

run().catch((e) => {
  console.error('SMOKE FAIL', e);
  process.exitCode = 1;
});
