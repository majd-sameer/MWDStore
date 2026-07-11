import { expect, test } from '@playwright/test';
import { loginAsAdmin } from './helpers';

/**
 * Bilingual product form + console-wide language toggle. The Arabic name
 * field must be RTL and the English one LTR regardless of the UI language;
 * the topbar toggle flips the whole document direction.
 */
test.describe('admin i18n / bidi', () => {
  test.slow();

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page); // pins the atb_lang cookie to 'en'
  });

  test('product 1390: #name is RTL, #nameEn is LTR; topbar toggle flips to Arabic and back', async ({
    page,
  }) => {
    await page.goto('/products/1390');

    const arabicName = page.locator('#name');
    const englishName = page.locator('#nameEn');
    await expect(arabicName).toBeVisible({ timeout: 30_000 });
    await expect(arabicName).toHaveAttribute('dir', 'rtl');
    await expect(englishName).toHaveAttribute('dir', 'ltr');

    // Starting point: English UI, LTR document.
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');

    // Switch to Arabic via the topbar button (labelled العربية in EN mode).
    await page
      .locator('.topbar .lang-switch', { hasText: 'العربية' })
      .click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.locator('html')).toHaveAttribute('lang', 'ar');

    // Dashboard nav item now reads لوحة التحكم.
    await expect(
      page.locator('.admin-nav').getByRole('link', { name: 'لوحة التحكم' }),
    ).toBeVisible();

    // Per-field directions are fixed by content language, not UI language.
    await expect(arabicName).toHaveAttribute('dir', 'rtl');
    await expect(englishName).toHaveAttribute('dir', 'ltr');

    // Switch back to English (button now says English).
    await page
      .locator('.topbar .lang-switch', { hasText: 'English' })
      .click();
    await expect(page.locator('html')).toHaveAttribute('dir', 'ltr');
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect(
      page.locator('.admin-nav').getByRole('link', { name: 'Dashboard' }),
    ).toBeVisible();
  });
});
