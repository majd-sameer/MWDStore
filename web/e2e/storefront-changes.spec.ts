import { expect, test, type Page } from '@playwright/test';
import { expectNoBodyHScroll, setLangCookie } from './helpers';

/**
 * Storefront changes shipped this round (EN mode throughout):
 * - responsive home/shop; the category strip is hidden below 768px (categories
 *   live in the hamburger drawer there) and wraps without overflow from 768 up,
 * - new money format "JOD 13.000" (space after the currency code),
 * - English variation names on product 1390,
 * - public order tracking by tracking number with card-style item rows on phones.
 */

const STOREFRONT = 'http://localhost:4200';

test.describe('storefront changes', () => {
  test.slow();

  test.beforeEach(async ({ context }) => {
    // Pin the UI language to English — it persists via the atb_lang cookie.
    await setLangCookie(context, 'en', STOREFRONT);
  });

  async function expectResponsive(page: Page, path: string): Promise<void> {
    for (const viewport of [
      { width: 375, height: 812 },
      { width: 768, height: 1024 },
    ]) {
      await page.setViewportSize(viewport);
      await page.goto(path);
      await expect(page.locator('app-header .site-header')).toBeVisible({
        timeout: 30_000,
      });
      await expectNoBodyHScroll(page);
    }
  }

  test('home and /shop have no horizontal scroll at 375 and 768; category strip hides on phones and wraps from 768 up', async ({
    page,
  }) => {
    await expectResponsive(page, '/');
    await expectResponsive(page, '/shop');

    // Category strip: load once at 768 (so the categories have rendered) …
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto('/');
    const strip = page.locator('nav.sub-nav');
    await expect(strip).toBeVisible({ timeout: 30_000 });
    await expect(strip.locator('.sub-link').first()).toBeVisible();

    // … then verify at 768 and ~993 it stays visible and WRAPS (no sideways
    // overflow of the nav, no body scroll)…
    for (const width of [768, 993]) {
      await page.setViewportSize({ width, height: 1024 });
      await expect(strip).toBeVisible();
      const { scrollWidth, clientWidth } = await strip.evaluate((el) => ({
        scrollWidth: el.scrollWidth,
        clientWidth: el.clientWidth,
      }));
      expect(
        scrollWidth,
        `category strip must wrap its links, not scroll sideways (at ${width}px)`,
      ).toBeLessThanOrEqual(clientWidth);
      await expectNoBodyHScroll(page);
    }

    // … and BELOW 768 the strip is hidden entirely (categories live in the
    // hamburger drawer on phones).
    await page.setViewportSize({ width: 375, height: 812 });
    await expect(strip).toBeHidden();
    await expectNoBodyHScroll(page);
  });

  test('product 1390: money format has a space after JOD and variation options are English', async ({
    page,
  }) => {
    await page.goto('/products/1390');

    const price = page.locator('.pdp-now');
    await expect(price).toBeVisible({ timeout: 30_000 });
    // New money format: "JOD 13.000" — currency code, space, amount.
    await expect(price).toHaveText(/JOD\s[\d,.]+/);

    // Variation option buttons (3 variations: large/small/med) show the
    // English names in EN mode — not the Arabic «مهباش».
    const options = page.locator('.pdp-variations button');
    await expect(options).toHaveCount(3, { timeout: 20_000 });
    for (const text of await options.allInnerTexts()) {
      expect(text).toContain('Traditional Coffee Mortar');
      expect(text).not.toContain('مهباش');
    }
  });

  test('track order 117319 shows order #30 with a card-style items table at 375', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('/track-order');

    const numberInput = page.locator('#tk-num');
    await expect(numberInput).toBeVisible({ timeout: 30_000 });
    await numberInput.fill('117319');
    await page.getByRole('button', { name: /Track/i }).click();

    // Result hero identifies the order the tracking number belongs to.
    await expect(page.locator('.hero-order')).toContainText('#30', {
      timeout: 30_000,
    });

    // Items table renders card-style rows on a phone viewport.
    const firstRow = page
      .locator('app-order-detail-view table.table-cards tbody tr')
      .first();
    await expect(firstRow).toBeVisible();
    const display = await firstRow.evaluate(
      (el) => getComputedStyle(el).display,
    );
    expect(display).toBe('block');
  });
});
