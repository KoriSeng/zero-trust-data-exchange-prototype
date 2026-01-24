import { generateKeyPair, exportJWK } from 'jose';

const keyCache = new Map();

export async function getIssuerSigningKey(issuerBaseUrl) {
  if (keyCache.has(issuerBaseUrl)) return keyCache.get(issuerBaseUrl);

  const { privateKey, publicKey } = await generateKeyPair('RS256');
  const jwk = await exportJWK(publicKey);
  jwk.use = 'sig';
  jwk.alg = 'RS256';
  jwk.kid = `kid-${Buffer.from(issuerBaseUrl).toString('base64url').slice(0, 12)}`;

  const material = { privateKey, publicJwk: jwk };
  keyCache.set(issuerBaseUrl, material);
  return material;
}
