import { expect, test } from '@playwright/test';
import { loginAsAdmin } from './helpers';

/**
 * The "Orders needing action" queue must humanize order statuses
 * ("Payment received"), never leak raw enum names ("PaymentReceived").
 */
test.describe('admin dashboard labels', () => {
  test.slow();

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test('action-queue status badges are humanized, not raw enum names', async ({
    page,
  }) => {
    const card = page.locator('.card', { hasText: 'Orders needing action' });
    await expect(card).toBeVisible({ timeout: 30_000 });

    // Wait for the stats to land: either rows or the explicit all-clear copy.
    const rows = card.locator('tbody tr');
    const allClear = card.getByText('No open orders — all caught up.');
    await expect(rows.first().or(allClear)).toBeVisible({ timeout: 30_000 });

    if (await allClear.isVisible()) {
      test.info().annotations.push({
        type: 'note',
        description:
          'Action queue was empty — no status labels to assert against.',
      });
      return;
    }

    const badges = rows.locator('.badge');
    const count = await badges.count();
    expect(count).toBeGreaterThan(0);

    const rawEnum = /PaymentReceived|PendingPayment|PaymentFailed/;
    for (const text of await badges.allInnerTexts()) {
      expect(text.trim().length).toBeGreaterThan(0);
      expect(text, `status badge "${text}" must be humanized`).not.toMatch(
        rawEnum,
      );
    }
  });
});
