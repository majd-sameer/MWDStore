import { expect, test } from '@playwright/test';
import { confirmModal, loginAsAdmin, trackNativeDialogs } from './helpers';

/**
 * The delete confirmation is an in-app ng-bootstrap modal (ConfirmService),
 * not a native window.confirm(). Cancelling must leave the row untouched.
 */
test.describe('admin confirm modal', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test('category delete opens an in-app modal (no native dialog) and cancel keeps the row', async ({
    page,
  }) => {
    const nativeDialogs = trackNativeDialogs(page);

    await page.goto('/categories');
    const rows = page.locator('table tbody tr');
    await expect(rows.first()).toBeVisible({ timeout: 30_000 });
    const rowCount = await rows.count();
    expect(rowCount).toBeGreaterThan(0);

    const firstRow = rows.first();
    const categoryName = (
      await firstRow.locator('td').first().locator('a').innerText()
    ).trim();
    expect(categoryName.length).toBeGreaterThan(0);

    await firstRow.getByTitle('Delete').click();

    // In-app modal: .modal element with dialog role, containing the name.
    const modal = confirmModal(page);
    await expect(modal).toBeVisible();
    await expect(modal).toHaveAttribute('role', 'dialog');
    await expect(modal.locator('.modal-body')).toContainText(categoryName);

    // No NATIVE browser dialog fired.
    expect(nativeDialogs()).toEqual([]);

    // Cancel — modal closes, row still present, count unchanged.
    await modal
      .locator('.modal-footer')
      .getByRole('button', { name: 'Cancel', exact: true })
      .click();
    await expect(modal).toHaveCount(0);
    await expect(
      rows.first().locator('td').first().locator('a'),
    ).toHaveText(new RegExp(categoryName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')));
    await expect(rows).toHaveCount(rowCount);

    expect(nativeDialogs()).toEqual([]);
  });
});
