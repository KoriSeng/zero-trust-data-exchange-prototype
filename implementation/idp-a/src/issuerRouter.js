import express from 'express';
import crypto from 'crypto';
import { SignJWT, decodeJwt } from 'jose';

import { getIssuerSigningKey } from './issuerConfig.js';
import { findUser } from './users.js';
import { renderLoginPage } from './templates.js';

export function buildIssuerRouter({ issuerDisplayName, userSet }) {
  const router = express.Router();

  // In-memory auth codes.
  const codes = new Map();

  router.get('/.well-known/openid-configuration', (req, res) => {
    const issuer = getBaseUrl(req);

    res.json({
      issuer,
      authorization_endpoint: `${issuer}/authorize`,
      token_endpoint: `${issuer}/token`,
      jwks_uri: `${issuer}/jwks`,
      userinfo_endpoint: `${issuer}/userinfo`,
      response_types_supported: ['code'],
      subject_types_supported: ['public'],
      id_token_signing_alg_values_supported: ['RS256'],
      scopes_supported: ['openid', 'profile', 'email'],
      claims_supported: ['sub', 'name', 'email', 'preferred_username', 'iss'],
      token_endpoint_auth_methods_supported: ['client_secret_post', 'client_secret_basic']
    });
  });

  router.get('/jwks', async (req, res) => {
    const issuer = getBaseUrl(req);
    const { publicJwk } = await getIssuerSigningKey(issuer);
    res.json({ keys: [publicJwk] });
  });

  router.get('/authorize', (req, res) => {
    const { client_id, redirect_uri, state, scope, response_type } = req.query;

    if (response_type !== 'code') {
      return res.status(400).type('text').send('Only response_type=code is supported.');
    }
    if (!client_id || !redirect_uri) {
      return res.status(400).type('text').send('client_id and redirect_uri are required.');
    }

    res.type('html').send(renderLoginPage({
      issuerDisplayName,
      client_id,
      redirect_uri,
      state,
      scope: scope ?? 'openid',
      response_type,
      error: null
    }));
  });

  router.post('/login', async (req, res) => {
    const { username, password, client_id, redirect_uri, state, scope, response_type } = req.body;

    const user = findUser(username);
    if (!user || user.password !== password) {
      return res.status(401).type('html').send(renderLoginPage({
        issuerDisplayName,
        client_id,
        redirect_uri,
        state,
        scope,
        response_type,
        error: 'Invalid username or password.'
      }));
    }

    if (user.status !== 'active') {
      return res.status(403).type('html').send(renderLoginPage({
        issuerDisplayName,
        client_id,
        redirect_uri,
        state,
        scope,
        response_type,
        error: 'User is deactivated.'
      }));
    }

    const code = crypto.randomBytes(16).toString('hex');
    codes.set(code, {
      issuer: getBaseUrl(req),
      client_id,
      redirect_uri,
      scope: scope ?? 'openid',
      sub: user.sub,
      displayName: user.displayName,
      username: user.username,
      email: user.email ?? `${user.username}@example.com`,
      createdAt: Date.now(),
      userSet
    });

    const redirect = normalizeRedirectUri(String(redirect_uri));
    redirect.searchParams.set('code', code);
    if (state) redirect.searchParams.set('state', String(state));

    return res.redirect(302, redirect.toString());
  });

  router.post('/token', async (req, res) => {
    const { grant_type, code, redirect_uri, client_id, client_secret } = req.body;
    let resolvedClientId = client_id;
    let resolvedClientSecret = client_secret;
    const authHeader = req.headers.authorization;

    if ((!resolvedClientId || !resolvedClientSecret) && typeof authHeader === 'string' && authHeader.startsWith('Basic ')) {
      const decoded = Buffer.from(authHeader.slice(6), 'base64').toString('utf8');
      const separator = decoded.indexOf(':');
      if (separator > -1) {
        resolvedClientId = decoded.slice(0, separator);
        resolvedClientSecret = decoded.slice(separator + 1);
      }
    }

    if (grant_type !== 'authorization_code') {
      return res.status(400).json({ error: 'unsupported_grant_type' });
    }

    if (!resolvedClientId || !resolvedClientSecret) {
      return res.status(401).json({ error: 'invalid_client' });
    }

    const record = codes.get(code);
    if (!record) {
      return res.status(400).json({ error: 'invalid_grant' });
    }

    if (redirect_uri && String(redirect_uri) !== String(record.redirect_uri)) {
      return res.status(400).json({ error: 'invalid_grant' });
    }
    if (String(resolvedClientId) !== String(record.client_id)) {
      return res.status(400).json({ error: 'invalid_grant' });
    }

    codes.delete(code);

    const issuer = record.issuer;
    const { privateKey, publicJwk } = await getIssuerSigningKey(issuer);

    const now = Math.floor(Date.now() / 1000);
    const idToken = await new SignJWT({
      name: record.displayName,
      preferred_username: record.username,
      email: record.email
    })
      .setProtectedHeader({ alg: 'RS256', kid: publicJwk.kid, typ: 'JWT' })
      .setIssuedAt(now)
      .setExpirationTime(now + 60 * 60)
      .setIssuer(issuer)
      .setAudience(resolvedClientId)
      .setSubject(record.sub)
      .sign(privateKey);

    const accessToken = await new SignJWT({
      scope: record.scope,
      preferred_username: record.username,
      name: record.displayName,
      email: record.email
    })
      .setProtectedHeader({ alg: 'RS256', kid: publicJwk.kid, typ: 'JWT' })
      .setIssuedAt(now)
      .setExpirationTime(now + 60 * 30)
      .setIssuer(issuer)
      .setAudience(resolvedClientId)
      .setSubject(record.sub)
      .sign(privateKey);

    return res.json({
      token_type: 'Bearer',
      expires_in: 3600,
      access_token: accessToken,
      id_token: idToken
    });
  });

  router.get('/userinfo', (req, res) => {
    const authHeader = req.headers.authorization ?? '';
    if (!String(authHeader).startsWith('Bearer ')) {
      return res.status(401).json({ error: 'invalid_token' });
    }

    try {
      const payload = decodeJwt(String(authHeader).slice(7));
      return res.json({
        sub: payload.sub,
        name: payload.name,
        email: payload.email,
        preferred_username: payload.preferred_username
      });
    } catch {
      return res.status(401).json({ error: 'invalid_token' });
    }
  });

  function getBaseUrl(req) {
    if (process.env.ISSUER_URL) {
      return process.env.ISSUER_URL;
    }
    const proto = (req.headers['x-forwarded-proto'] ?? req.protocol);
    const host = req.headers['x-forwarded-host'] ?? req.get('host');
    return `${proto}://${host}`;
  }

  function normalizeRedirectUri(rawRedirectUri) {
    const redirect = new URL(rawRedirectUri);
    // LocalStack may send zero-trust-local:4566 for idpresponse callbacks, which is not browser-routable.
    if (redirect.host === 'zero-trust-local:4566') {
      redirect.protocol = 'https:';
      redirect.host = 'localhost.localstack.cloud:4566';
    }
    return redirect;
  }

  return router;
}
