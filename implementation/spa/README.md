# Zero Trust Data Exchange SPA

React + Vite single-page application for the zero-trust data exchange prototype.

## What this SPA covers

- Cognito Hosted UI login with IDP-A and IDP-B redirect buttons.
- Requester flow: submit request, monitor statuses, redeem OTP to obtain pre-signed URLs.
- Data owner flow: review pending requests, approve (issue OTP), and reject with reason.
- Playwright smoke tests for login rendering, route guard behaviour, and optional backend health check.

## Prerequisites

| Tool | Version |
| --- | --- |
| Node.js | 18+ |
| npm | 9+ |
| Terraform/OpenTofu | 1.0+ (for infrastructure outputs) |

## Setup

1. Install dependencies:

   ```powershell
   cd implementation\spa
   npm install
   ```

2. Copy the environment template:

   ```powershell
   Copy-Item .env.example .env
   ```

3. Fill `.env` values from Terraform outputs:

| Variable | Terraform output |
| --- | --- |
| `VITE_COGNITO_USER_POOL_ID` | `cognito_user_pool_id` |
| `VITE_COGNITO_APP_CLIENT_ID` | `cognito_app_client_id` |
| `VITE_COGNITO_DOMAIN` | `cognito_hosted_ui_url` (domain only) |
| `VITE_API_BASE_URL` | `backend_api_url` |

You can also set optional redirect lists:

- `VITE_COGNITO_REDIRECT_SIGN_IN`
- `VITE_COGNITO_REDIRECT_SIGN_OUT`

Both accept comma-separated URLs to support localhost + CloudFront.

## Run

```powershell
npm run dev
```

App default URL: `http://localhost:5173`

## Build

```powershell
npm run build
```

Build output: `dist/`

## Deploy build to S3 + CloudFront

```powershell
npm run build
$bucket = terraform -chdir=..\..\infrastructure output -raw spa_bucket_name
$distId = terraform -chdir=..\..\infrastructure output -raw spa_cloudfront_distribution_id
aws s3 sync dist/ "s3://$bucket/" --delete --exclude "assets/*" --cache-control "public,max-age=300,must-revalidate"
aws s3 sync dist\assets\ "s3://$bucket/assets/" --delete --cache-control "public,max-age=31536000,immutable"
aws s3 cp dist\index.html "s3://$bucket/index.html" --cache-control "no-store,max-age=0" --content-type "text/html"
aws cloudfront create-invalidation --distribution-id $distId --paths "/*"
```

This keeps `index.html` and SPA route fallbacks fresh while allowing hashed bundle assets to stay cached for performance.

## Tests

Install browser binaries once:

```powershell
npx playwright install chromium firefox
```

Run smoke suite:

```powershell
npm run test:e2e
```

Notes:

- Full automated Cognito login is intentionally out of scope for this prototype.
- Health check smoke test runs only when `VITE_API_BASE_URL` or `API_BASE_URL` is set.

## Project structure

```text
src/
├── api/            # API client wrapper with bearer token injection
├── components/     # Route guard, layout, approval/redeem UI
├── config/         # Amplify + env processing
├── contexts/       # Authentication context and session state
└── pages/          # Login, callback, dashboard, requester and approver screens
tests/
└── smoke.spec.js   # Playwright smoke tests
```
