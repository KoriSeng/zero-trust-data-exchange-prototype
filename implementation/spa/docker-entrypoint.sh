#!/bin/sh
# Waits for LocalStack to write Cognito config, generates .env.local, then starts Vite.
set -e

CONFIG_FILE="/localstack-data/cognito-config.env"
MAX_WAIT=120
WAITED=0

echo "Waiting for Cognito configuration (max ${MAX_WAIT}s)..."
while [ ! -f "$CONFIG_FILE" ] && [ "$WAITED" -lt "$MAX_WAIT" ]; do
  sleep 3
  WAITED=$((WAITED + 3))
done

if [ ! -f "$CONFIG_FILE" ]; then
  echo "WARNING: Cognito config not found after ${MAX_WAIT}s. SPA will start without auth configuration."
else
  # shellcheck source=/dev/null
  . "$CONFIG_FILE"
  cat > /app/.env.local <<EOF
VITE_COGNITO_USER_POOL_ID=${USER_POOL_ID}
VITE_COGNITO_APP_CLIENT_ID=${CLIENT_ID}
VITE_COGNITO_DOMAIN=${COGNITO_DOMAIN}
VITE_COGNITO_REDIRECT_SIGN_IN=http://localhost:5173/callback
VITE_COGNITO_REDIRECT_SIGN_OUT=http://localhost:5173
VITE_API_BASE_URL=http://localhost:5000
EOF
  echo "SPA environment written from Cognito config."
fi

exec npm run dev -- --host
