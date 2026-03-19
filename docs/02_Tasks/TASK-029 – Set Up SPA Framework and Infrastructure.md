---
type: Task
task_id: TASK-029
title: "Set Up SPA Framework and Infrastructure"
owner: STK-001
status: "In Progress"
related_milestone: MS-009
---

## Description

Bootstrap a React 18 + Vite SPA inside `implementation/spa/`, wire up AWS Amplify v6 for Cognito auth configuration, and provision the S3 + CloudFront hosting stack via Terraform. This task produces the empty-but-running shell that all subsequent SPA tasks build on.

## Implementation Notes

### 1. Bootstrap the Vite project

Run the following from inside `implementation/`:

```bash
npm create vite@latest spa -- --template react
cd spa
npm install
```

This creates `implementation/spa/` with the standard Vite + React scaffold (`src/main.jsx`, `src/App.jsx`, `vite.config.js`, etc.).

### 2. Install Amplify auth library

```bash
npm install aws-amplify
```

`aws-amplify` v6 is the preferred Cognito integration for this POC — it handles the Authorization Code + PKCE redirect flow and token storage in `localStorage` with no extra configuration.

### 3. Install Tailwind CSS (optional — keep it simple)

Tailwind is optional for a POC. If you want it:

```bash
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init -p
```

Then add `@tailwind base; @tailwind components; @tailwind utilities;` to `src/index.css` and set `content: ["./index.html", "./src/**/*.{js,jsx}"]` in `tailwind.config.js`. Plain CSS modules are a perfectly fine alternative.

### 4. Environment variables

Create `implementation/spa/.env.example` with the following variables (commit this file; never commit `.env`):

```
VITE_COGNITO_USER_POOL_ID=us-east-1_XXXXXXXXX
VITE_COGNITO_APP_CLIENT_ID=XXXXXXXXXXXXXXXXXXXXXXXXXX
VITE_COGNITO_DOMAIN=zero-trust-prototype-dev-XXXXXXXX.auth.us-east-1.amazoncognito.com
VITE_API_BASE_URL=https://XXXXXXXXXX.execute-api.us-east-1.amazonaws.com
```

Values come from Terraform outputs: `cognito_user_pool_id`, `cognito_app_client_id`, `cognito_hosted_ui_url`, and `backend_api_url`. Copy to `.env` locally and fill in. Add `.env` to `.gitignore`.

### 5. Amplify configuration in `src/main.jsx`

Replace the default `main.jsx` content with:

```jsx
import React from 'react';
import ReactDOM from 'react-dom/client';
import { Amplify } from 'aws-amplify';
import App from './App';
import './index.css';

Amplify.configure({
  Auth: {
    Cognito: {
      userPoolId: import.meta.env.VITE_COGNITO_USER_POOL_ID,
      userPoolClientId: import.meta.env.VITE_COGNITO_APP_CLIENT_ID,
      loginWith: {
        oauth: {
          domain: import.meta.env.VITE_COGNITO_DOMAIN,
          scopes: ['openid', 'email', 'profile'],
          redirectSignIn: ['http://localhost:5173/callback'],
          redirectSignOut: ['http://localhost:5173'],
          responseType: 'code'
        }
      }
    }
  }
});

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
```

> **Note:** When deploying to CloudFront, add the CloudFront URL to both `redirectSignIn` and `redirectSignOut` arrays. Also update the Cognito App Client `callback_urls` in the Terraform Cognito module accordingly.

### 6. React Router v6 skeleton

Install routing:

```bash
npm install react-router-dom
```

Set up three top-level routes in `src/App.jsx`:

```jsx
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import Login    from './pages/Login';
import Callback from './pages/Callback';
import Dashboard from './pages/Dashboard';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login"    element={<Login />} />
        <Route path="/callback" element={<Callback />} />
        <Route path="/*"        element={<Dashboard />} />
      </Routes>
    </BrowserRouter>
  );
}
```

Create stub files for `src/pages/Login.jsx`, `src/pages/Callback.jsx`, and `src/pages/Dashboard.jsx` — they can just return a `<div>` with the page name for now. TASK-030 fills them in.

### 7. Terraform — S3 + CloudFront hosting

Add the following resources to `infrastructure/main.tf` (or a new `infrastructure/spa.tf`):

```hcl
# S3 bucket — private, served only through CloudFront
resource "aws_s3_bucket" "spa" {
  bucket = "${var.project_name}-spa-${var.environment}"
}

resource "aws_s3_bucket_public_access_block" "spa" {
  bucket                  = aws_s3_bucket.spa.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# CloudFront Origin Access Control
resource "aws_cloudfront_origin_access_control" "spa" {
  name                              = "${var.project_name}-spa-oac"
  origin_access_control_origin_type = "s3"
  signing_behavior                  = "always"
  signing_protocol                  = "sigv4"
}

# CloudFront distribution
resource "aws_cloudfront_distribution" "spa" {
  enabled             = true
  default_root_object = "index.html"

  origin {
    domain_name              = aws_s3_bucket.spa.bucket_regional_domain_name
    origin_id                = "spa-s3-origin"
    origin_access_control_id = aws_cloudfront_origin_access_control.spa.id
  }

  default_cache_behavior {
    target_origin_id       = "spa-s3-origin"
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD"]
    cached_methods         = ["GET", "HEAD"]

    forwarded_values {
      query_string = false
      cookies { forward = "none" }
    }
  }

  # SPA routing — return index.html for all 404s so React Router handles navigation
  custom_error_response {
    error_code         = 404
    response_code      = 200
    response_page_path = "/index.html"
  }

  restrictions {
    geo_restriction { restriction_type = "none" }
  }

  viewer_certificate {
    cloudfront_default_certificate = true
  }
}

output "spa_cloudfront_url" {
  value = "https://${aws_cloudfront_distribution.spa.domain_name}"
}
```

Also add the CloudFront URL and localhost callback to the Cognito App Client in the Cognito module's `callback_urls` list.

> **Cost note:** CloudFront costs ~$0.0085 per 10k requests. Entirely acceptable for a prototype demo with low traffic.

### 8. Deploy build to S3

After `terraform apply` resolves the bucket name:

```bash
npm run build
aws s3 sync dist/ s3://$(terraform -chdir=../../infrastructure output -raw spa_bucket_name)/ --delete
aws cloudfront create-invalidation \
  --distribution-id $(terraform -chdir=../../infrastructure output -raw spa_cloudfront_distribution_id) \
  --paths "/*"
```

Add these as `package.json` scripts for convenience (`"deploy": "npm run build && ..."` ).

## Definition of Done

- [ ] `npm run dev` starts the Vite dev server and the app loads at `http://localhost:5173`
- [ ] `src/main.jsx` configures Amplify using `VITE_*` env variables
- [ ] `.env.example` committed with all four required variable names
- [ ] React Router routes stubbed for `/login`, `/callback`, and `/*`
- [ ] Terraform resources for S3 bucket and CloudFront distribution added (plan passes)
- [ ] Cognito App Client `callback_urls` updated to include `http://localhost:5173/callback`
- [ ] `npm run build` produces a `dist/` folder without errors
