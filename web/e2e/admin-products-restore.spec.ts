import { expect, test, type Page } from '@playwright/test';
import { confirmModal, loginAsAdmin } from './helpers';

/**
 * Soft-delete / restore round-trip on the products list. Product 1395 is the
 * seeded soft-deleted test product (name contains «تجريبي»). The test restores
 * it, then re-deletes it, so the DB ends in the same state it started in and
 * the suite is re-runnable.
 */
const PRODUCT_ID = 1395;

function productRow(page: Page) {
  return page.locator('table tbody tr', { hasText: `#${PRODUCT_ID}` });
}

async function clickStatusChip(page: Page, label: string): Promise<void> {
  await page.locator('.filter-chip', { hasText: label }).click();
}

async function waitForListSettled(page: Page): Promise<void> {
  await expect(page.locator('.spinner-border')).toHaveCount(0, {
    timeout: 30_000,
  });
}

test.describe('admin products restore', () => {
  test.slow();

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/products');
    await expect(
      page.getByRole('heading', { name: 'Products', exact: true }),
    ).toBeVisible({ timeout: 30_000 });
  });

  test('restore product 1395 from the Deleted filter, then re-delete it (self-restoring)', async ({
    page,
  }) => {
    // --- Deleted filter shows the soft-deleted product with a Restore action ---
    await clickStatusChip(page, 'Deleted');
    await waitForListSettled(page);

    const row = productRow(page);
    await expect(row).toHaveCount(1, { timeout: 20_000 });
    await expect(row.locator('.badge', { hasText: 'Deleted' })).toBeVisible();

    const restoreButton = row.getByTitle('Restore');
    await expect(restoreButton).toBeVisible();
    await restoreButton.click();

    let modal = confirmModal(page);
    await expect(modal).toBeVisible();
    await modal.getByRole('button', { name: 'Restore', exact: true }).click();
    await expect(
      page.locator('ngb-toast', { hasText: 'Product restored.' }),
    ).toBeVisible({ timeout: 20_000 });

    // The list reloads; the restored product leaves the Deleted segment.
    await expect(productRow(page)).toHaveCount(0, { timeout: 20_000 });

    // --- Re-delete it (it comes back as an unpublished Draft) ---
    await clickStatusChip(page, 'Draft');
    await waitForListSettled(page);
    let restoredRow = productRow(page);
    if ((await restoredRow.count()) === 0) {
      // Safety net: if the product was stored as published, fall back to All.
      await clickStatusChip(page, 'All');
      await waitForListSettled(page);
      restoredRow = productRow(page);
    }
    await expect(restoredRow).toHaveCount(1, { timeout: 20_000 });

    await restoredRow.getByTitle('Delete').click();
    modal = confirmModal(page);
    await expect(modal).toBeVisible();
    await modal.getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(
      page.locator('ngb-toast', { hasText: 'Product deleted.' }),
    ).toBeVisible({ timeout: 20_000 });

    // --- Verify it's back under Deleted ---
    await clickStatusChip(page, 'Deleted');
    await waitForListSettled(page);
    await expect(productRow(page)).toHaveCount(1, { timeout: 20_000 });
  });
});
