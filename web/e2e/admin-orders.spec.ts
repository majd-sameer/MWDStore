import { expect, test } from '@playwright/test';
import { confirmModal, loginAsAdmin, parseMoney } from './helpers';

/**
 * Order-detail admin actions shipped this round: humanized status select with a
 * change-gated Apply button, the partial-refund card (captured / refunded /
 * refundable + refund action), and the in-app cancel confirmation.
 *
 * Data facts (local dev DB): order 29 is Shipped; order 25 is PaymentReceived
 * with JOD 13.000 captured via Stripe. Each full run of this suite refunds
 * JOD 1.000 more of order 25 — once refundable hits 0 the test asserts the
 * fully-refunded message instead.
 */
test.describe('admin order detail', () => {
  test.slow();

  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test('order 29: status select shows Shipped and Apply is disabled until the selection changes', async ({
    page,
  }) => {
    await page.goto('/orders/29');

    const statusCard = page
      .locator('.card', { has: page.getByText('Update status', { exact: true }) })
      .last();
    const select = statusCard.locator('select');
    await expect(select).toBeVisible({ timeout: 30_000 });

    // The order itself is Shipped (header badge).
    await expect(page.locator('.badge.fs-6')).toHaveText(/Shipped/);

    // Fixed: options now carry [selected] per option (a [value] binding on the
    // select applied before the @for options existed, falling back to "New"),
    // so the select displays the order's real status.
    await expect(select.locator('option:checked')).toHaveText(/Shipped/);

    // Apply is disabled while the selection matches the order's real status —
    // proof the bound model correctly holds Shipped.
    const apply = statusCard.getByRole('button', { name: 'Apply', exact: true });
    await expect(apply).toBeDisabled();

    // Changing the selection arms Apply; restoring it disarms again. (No click —
    // this test must not mutate the order.)
    await select.selectOption({ label: 'Complete' });
    await expect(apply).toBeEnabled();
    await select.selectOption({ label: 'Shipped' });
    await expect(apply).toBeDisabled();
  });

  test('order 25: refund card shows amounts and processes a JOD 1.000 refund (or shows fully-refunded)', async ({
    page,
  }) => {
    await page.goto('/orders/25');

    const refundCard = page
      .locator('.card', { has: page.getByText('Refund', { exact: true }) })
      .last();
    await expect(refundCard).toBeVisible({ timeout: 30_000 });

    const readAmounts = async () => {
      const dds = refundCard.locator('dl dd');
      await expect(dds).toHaveCount(3);
      const [captured, refunded, refundable] = await dds.allInnerTexts();
      return {
        captured: parseMoney(captured),
        refunded: parseMoney(refunded),
        refundable: parseMoney(refundable),
      };
    };

    const before = await readAmounts();
    expect(before.captured).toBeGreaterThan(0);
    expect(before.captured).toBeCloseTo(before.refunded + before.refundable, 3);

    if (before.refundable > 0) {
      await refundCard.locator('#refund-amount').fill('1');
      await refundCard.getByRole('button', { name: 'Refund', exact: true }).click();

      const modal = confirmModal(page);
      await expect(modal).toBeVisible();
      await modal.getByRole('button', { name: 'Refund', exact: true }).click();

      await expect(
        page.locator('ngb-toast', { hasText: 'Refund processed.' }),
      ).toBeVisible({ timeout: 20_000 });

      // Reload and verify the refunded total grew by exactly 1.000.
      await page.reload();
      await expect(refundCard.locator('dl dd').first()).toBeVisible({
        timeout: 30_000,
      });
      const after = await readAmounts();
      expect(after.refunded).toBeCloseTo(before.refunded + 1, 3);
      expect(after.refundable).toBeCloseTo(before.refundable - 1, 3);
    } else {
      // Re-run state: everything captured has been refunded by earlier runs.
      await expect(
        refundCard.getByText('This order has been fully refunded.'),
      ).toBeVisible();
    }
  });

  test('order 25: cancel-order opens the in-app modal and dismissing it does nothing', async ({
    page,
  }) => {
    await page.goto('/orders/25');

    const cancelButton = page.getByRole('button', {
      name: 'Cancel order (restock)',
    });
    await expect(cancelButton).toBeVisible({ timeout: 30_000 });
    await cancelButton.click();

    const modal = confirmModal(page);
    await expect(modal).toBeVisible();
    await expect(modal.locator('.modal-body')).toContainText(
      'Cancel this order?',
    );

    // Dismiss — do NOT cancel the order.
    await modal
      .locator('.modal-footer')
      .getByRole('button', { name: 'Cancel', exact: true })
      .click();
    await expect(modal).toHaveCount(0);

    // Order is still not cancelled: the cancel action stays available.
    await expect(cancelButton).toBeEnabled();
  });
});
