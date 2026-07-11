import { expect, test, type Page } from '@playwright/test';
import { expectNoBodyHScroll, loginAsAdmin } from './helpers';

/**
 * Phone layout (375×812) for the admin console: hamburger-driven sidebar
 * drawer, no horizontal scroll on the list pages, and card-style tables.
 */

async function sidebarBox(page: Page) {
  const box = await page.locator('.admin-sidebar').boundingBox();
  if (!box) {
    throw new Error('sidebar has no bounding box');
  }
  return box;
}

test.describe('admin mobile layout', () => {
  test.slow();

  test.beforeEach(async ({ page }) => {
    // Login happens at the default desktop size, then we shrink to a phone.
    await loginAsAdmin(page);
    await page.setViewportSize({ width: 375, height: 812 });
  });

  test('hamburger toggles the sidebar drawer with full labels', async ({
    page,
  }) => {
    const hamburger = page.locator('.menu-toggle');
    await expect(hamburger).toBeVisible();

    // Sidebar starts closed: parked off-screen, not covering content.
    // (Poll — the drawer slides with a 0.2s transition after the resize.)
    const sidebar = page.locator('.admin-sidebar');
    await expect(sidebar).not.toHaveClass(/open/);
    await expect
      .poll(async () => {
        const box = await sidebarBox(page);
        return box.x + box.width;
      })
      .toBeLessThanOrEqual(0);

    // Open: drawer slides in with full labels.
    await hamburger.click();
    await expect(sidebar).toHaveClass(/open/);
    const productsLink = sidebar.getByRole('link', {
      name: 'Products',
      exact: true,
    });
    await expect(productsLink).toBeVisible();
    await expect
      .poll(async () => (await sidebarBox(page)).x, { timeout: 5_000 })
      .toBe(0);

    // Backdrop click closes it again. The backdrop covers the whole viewport
    // and the 280px drawer sits on top of its left side — click the exposed
    // strip on the right.
    await page
      .locator('.drawer-backdrop')
      .click({ position: { x: 330, y: 400 } });
    await expect(sidebar).not.toHaveClass(/open/);
    await expect
      .poll(async () => {
        const box = await sidebarBox(page);
        return box.x + box.width;
      })
      .toBeLessThanOrEqual(0);
  });

  test('no horizontal scroll on /products, /orders, /settings at 375px', async ({
    page,
  }) => {
    for (const path of ['/products', '/orders']) {
      await page.goto(path);
      // Let the lazy route render its content before measuring.
      await expect(page.locator('h1').first()).toBeVisible({
        timeout: 30_000,
      });
      await expect(page.locator('.spinner-border')).toHaveCount(0, {
        timeout: 30_000,
      });
      await expectNoBodyHScroll(page);
    }

    // Fixed: long monospace setting keys now wrap inside the card cells
    // (overflow-wrap: anywhere in the table-cards CSS), so /settings holds the
    // same no-horizontal-scroll bar as every other admin page.
    await page.goto('/settings');
    await expect(page.locator('h1').first()).toBeVisible({ timeout: 30_000 });
    await expect(page.locator('.spinner-border')).toHaveCount(0, {
      timeout: 30_000,
    });
    await expectNoBodyHScroll(page);
  });

  test('products table renders card-style rows at 375px', async ({ page }) => {
    await page.goto('/products');
    const firstRow = page.locator('table.table-cards tbody tr').first();
    await expect(firstRow).toBeVisible({ timeout: 30_000 });
    const display = await firstRow.evaluate(
      (el) => getComputedStyle(el).display,
    );
    expect(display).toBe('block');
  });
});
