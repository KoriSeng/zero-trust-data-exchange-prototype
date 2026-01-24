# Simulated OIDC IdP-A

Minimal OIDC provider simulation (Issuer A) for MS-002.

## Run locally
```powershell
cd src\idp-a
npm install
npm run dev
```

Default port: `9001`

Issuer base URL:
- `http://localhost:9001`

Endpoints:
- `/.well-known/openid-configuration`
- `/jwks`
- `/authorize`
- `/login`
- `/token`

## Smoke test (no k6 required)
```powershell
cd src\idp-a
npm run test:smoke
```
