import { expect, test } from '@playwright/test';
import { loginAsAdmin } from './helpers';

/**
 * Account security page (TOTP MFA). The admin account has MFA off; starting
 * the setup renders the QR + shared key + confirmation code input, but the
 * status must stay "disabled" until a code is confirmed — this test never
 * enables MFA (that would lock the rest of the suite out of the console).
 */
test.describe('admin security (MFA)', () => {
  test.slow();

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test('MFA card shows disabled state; Set up reveals QR + shared key; reload keeps it disabled', async ({
    page,
  }) => {
    await page.goto('/security');
    await expect(
      page.getByRole('heading', { name: 'Security', exact: true }),
    ).toBeVisible({ timeout: 30_000 });

    const card = page.locator('.card', {
      hasText: 'Two-factor authentication (TOTP)',
    });
    await expect(card).toBeVisible();

    // Disabled state (MFA is off for this account).
    const setupButton = card.getByRole('button', { name: 'Set up' });
    await expect(
      card.getByText('Two-factor authentication is off', { exact: false }),
    ).toBeVisible({ timeout: 20_000 });
    await expect(setupButton).toBeVisible();

    // Start setup: QR code (data: URI), shared key and a code input appear.
    await setupButton.click();
    const qr = card.locator('img[alt="Two-factor setup QR code"]');
    await expect(qr).toBeVisible({ timeout: 20_000 });
    await expect(qr).toHaveAttribute('src', /^data:/);

    const sharedKey = card.locator('code');
    await expect(sharedKey).toBeVisible();
    expect((await sharedKey.innerText()).trim().length).toBeGreaterThan(0);

    await expect(card.locator('#sec-enable-code')).toBeVisible();
    // DO NOT enable — leaving setup unconfirmed must not change the status.

    // Reload: still disabled.
    await page.reload();
    await expect(
      page
        .locator('.card', { hasText: 'Two-factor authentication (TOTP)' })
        .getByText('Two-factor authentication is off', { exact: false }),
    ).toBeVisible({ timeout: 30_000 });
    await expect(
      page.getByRole('button', { name: 'Set up' }),
    ).toBeVisible();
  });
});
