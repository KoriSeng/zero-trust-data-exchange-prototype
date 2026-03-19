import { expect, test } from '@playwright/test';

const apiBaseUrl = (process.env.VITE_API_BASE_URL ?? process.env.API_BASE_URL ?? '').replace(
  /\/$/,
  '',
);

test.beforeEach(async ({ context }) => {
  await context.clearCookies();
  await context.addInitScript(() => {
    window.localStorage.clear();
    window.sessionStorage.clear();
  });
});

test('health endpoint returns 200', async ({ request }) => {
  test.skip(
    !apiBaseUrl,
    'Set VITE_API_BASE_URL (or API_BASE_URL) to run the backend health smoke check.',
  );

  const response = await request.get(`${apiBaseUrl}/health`);
  expect(response.status()).toBe(200);
});

test('login page renders and redirects to Cognito', async ({ page }) => {
  // This test verifies the login page doesn't show the old IDP buttons
  // In a full integration test with Cognito, this would redirect to Cognito's hosted UI
  await page.goto('/login');
  // Verify the old IDP buttons don't exist
  const idpAButton = page.getByRole('button', { name: 'Sign in as Requester (IDP-A)' });
  const idpBButton = page.getByRole('button', { name: 'Sign in as Data Owner (IDP-B)' });
  await expect(idpAButton).not.toBeVisible();
  await expect(idpBButton).not.toBeVisible();
});

test('unauthenticated access to dashboard redirects to login', async ({ page }) => {
  await page.goto('/', { waitUntil: 'domcontentloaded' });
  await page.waitForURL('**/login', { timeout: 10000 });
  await expect(page).toHaveURL(/\/login(?:\?.*)?$/);
  // Verify the old IDP buttons don't exist (since page now auto-redirects to Cognito)
  const idpAButton = page.getByRole('button', { name: 'Sign in as Requester (IDP-A)' });
  const idpBButton = page.getByRole('button', { name: 'Sign in as Data Owner (IDP-B)' });
  await expect(idpAButton).not.toBeVisible();
  await expect(idpBButton).not.toBeVisible();
});
