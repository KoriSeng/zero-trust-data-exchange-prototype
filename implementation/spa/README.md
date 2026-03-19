# Zero Trust Data Exchange SPA

React + Vite single-page application for the zero-trust data exchange prototype.

## What this SPA covers

- **Modern Card-Based UI**: Clean, professional interface suitable for bio research lab data sharing
- **Smart Auto-Refresh**: Automatic polling (25s) with page visibility detection to conserve resources
- **Visual Status Timeline**: Interactive timeline showing request progress through approval workflow
- **Cognito Authentication**: Hosted UI login with IDP-A and IDP-B redirect buttons
- **Requester Flow**: Submit requests, monitor status, redeem OTP, download files during active window
- **Data Owner Flow**: Review pending requests, approve (issue OTP), and reject with reason
- **Active Window Downloads**: Generate short-lived (60s) pre-signed URLs for file downloads
- **Responsive Design**: Mobile-friendly card layouts with expandable details
- **Real-time Updates**: Visual indicators showing last update time and refresh status
- **Playwright Tests**: Smoke tests for login rendering, route guards, and backend health

## UI Features

### Request Cards
- Card-based layout replacing traditional tables
- Visual status timeline showing workflow progress
- Expandable details panel for actions
- Skeleton loading states for smooth UX
- Empty states with helpful prompts

### Smart Polling
- Auto-refresh every 25 seconds
- Pauses when browser tab is inactive (Page Visibility API)
- Manual refresh button always available
- Visual indicator showing "Updated X seconds ago"
- Animated spinner during refresh

### File Downloads
- "View files" button for Redeemed requests
- Per-file download buttons with progress feedback
- 60-second pre-signed URL expiry
- Download link expires indicator


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
├── components/     # Route guard, layout, approval/redeem UI, timeline, auto-refresh
├── config/         # Amplify + env processing
├── contexts/       # Authentication context and session state
└── pages/          # Login, callback, dashboard, requester and approver screens
tests/
└── smoke.spec.js   # Playwright smoke tests
```

## Recent UI Improvements

### Two-Tier Navigation System
Professional navigation designed for research platform users:
- **Modern Header**:
  - 🧬 DNA icon and "Zero Trust Data Exchange" branding
  - User avatar with dropdown menu (profile details, sign out)
  - Notification bell and settings icons (ready for future features)
- **Primary Navigation Tabs**:
  - Dashboard, Data Explorer, My Requests, Pending Approvals
  - Icons with labels for quick recognition
  - Underline indicator for active page
  - Role-aware visibility (only shows sections you have access to)
- **Quick Actions Bar**:
  - "Browse Datasets" - jump directly to data catalog
  - "New Request" - start a data access request
  - "Review Queue" - for reviewers to see pending approvals
  - Context-aware (actions appear based on your role)
- **Responsive Design**: Mobile-friendly with smart collapsing

### OTP Email Modal
The "View OTP Email" feature now opens in a clean modal dialog:
- **Centered Modal**: Opens on top of current page with backdrop blur
- **Highlighted OTP Code**: Large, copy-friendly display with gradient background
- **Email Details**: Recipient, subject, expiration time clearly shown
- **Full Email Content**: Preview of the complete email text
- **Keyboard Support**: Press Escape to close, click outside to dismiss
- **Mobile-Friendly**: Full-screen on mobile devices for better readability

### Smart Auto-Refresh Polling
The "My Requests" page now automatically refreshes every 25 seconds to show the latest status updates:
- **Page Visibility Detection**: Polling pauses automatically when you switch tabs or minimize the browser
- **Live Update Indicator**: Shows "Updated just now" or "Updated 45s ago" so you know how current the data is
- **Animated Spinner**: Visual feedback during refresh operations

### Visual Status Timeline
Each request now displays a clean visual timeline showing workflow progress:
- ✅ **Completed stages** - shown with checkmarks
- 🔵 **Current stage** - highlighted with a pulsing dot
- ⚪ **Pending stages** - shown in gray
- 🏷️ **Terminal states** (Denied/Expired) - displayed as colored badges

### Card-Based Layout
Replaced the table view with modern card-based design:
- **Expandable Details**: Click "Show details" to see full request information
- **Inline Actions**: Actions appear right where you need them
- **Mobile-Friendly**: Cards adapt gracefully to smaller screens
- **Skeleton Loading**: Smooth loading states while data fetches

### Active Window Downloads
When a request is in "Redeemed" status and within its access window:
- Click "View files" to see available dataset files
- Each file has a "Download" button that generates a secure, short-lived URL
- Download links expire after 60 seconds for security
- All downloads are logged in the audit trail
