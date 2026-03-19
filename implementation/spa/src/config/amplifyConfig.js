const DEFAULT_SIGN_IN_REDIRECTS = ['http://localhost:5173/callback'];
const DEFAULT_SIGN_OUT_REDIRECTS = ['http://localhost:5173'];

function parseRedirects(rawValue, fallback) {
  if (!rawValue) {
    return fallback;
  }

  const parsed = rawValue
    .split(',')
    .map((value) => value.trim())
    .filter(Boolean);

  return parsed.length > 0 ? parsed : fallback;
}

function normalizeDomain(domain) {
  if (!domain) {
    return '';
  }

  return domain.replace(/^https?:\/\//i, '').replace(/\/$/, '');
}

export function getAmplifyConfig() {
  const userPoolId = import.meta.env.VITE_COGNITO_USER_POOL_ID;
  const userPoolClientId = import.meta.env.VITE_COGNITO_APP_CLIENT_ID;
  const cognitoDomain = normalizeDomain(import.meta.env.VITE_COGNITO_DOMAIN);

  if (!userPoolId || !userPoolClientId || !cognitoDomain) {
    console.warn(
      'Amplify configuration is incomplete. Update implementation/spa/.env before using authentication flows.',
    );
  }

  return {
    Auth: {
      Cognito: {
        userPoolId,
        userPoolClientId,
        loginWith: {
          oauth: {
            domain: cognitoDomain,
            scopes: ['openid', 'email', 'profile'],
            redirectSignIn: parseRedirects(
              import.meta.env.VITE_COGNITO_REDIRECT_SIGN_IN,
              DEFAULT_SIGN_IN_REDIRECTS,
            ),
            redirectSignOut: parseRedirects(
              import.meta.env.VITE_COGNITO_REDIRECT_SIGN_OUT,
              DEFAULT_SIGN_OUT_REDIRECTS,
            ),
            responseType: 'code',
          },
        },
      },
    },
  };
}
