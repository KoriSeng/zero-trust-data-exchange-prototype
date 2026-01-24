# Simulated OIDC IdP-B

Minimal OIDC provider simulation (Issuer B) for MS-002.

## Run locally
```powershell
cd src\idp-b
npm install
npm run dev
```

Default port: `9002`

Issuer base URL:
- `http://localhost:9002`

Endpoints:
- `/.well-known/openid-configuration`
- `/jwks`
- `/authorize`
- `/login`
- `/token`

## Smoke test (no k6 required)
```powershell
cd src\idp-b
npm run test:smoke
```
