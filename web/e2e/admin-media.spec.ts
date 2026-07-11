import { expect, test, type Page } from '@playwright/test';
import { confirmModal, loginAsAdmin } from './helpers';

/**
 * Media library: upload → appears with an "Unused" badge → Copy URL → delete
 * via the in-app modal. Fully self-cleaning: the uploaded file is removed at
 * the end, and any `e2e-tiny-*` orphans from an aborted earlier run are swept
 * first.
 *
 * Note: the backend stores uploads under a generated (GUID) filename — the
 * grid card shows that stored name, while the search box still matches the
 * ORIGINAL upload name. Cards are therefore located via the search filter,
 * not by their displayed filename.
 */

// Minimal valid 1×1 PNG.
const TINY_PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64',
);

/** Grid item cards (the page wrapper is also a .card — items are .h-100). */
function itemCards(page: Page) {
  return page.locator('.card.h-100');
}

async function searchFor(page: Page, term: string): Promise<void> {
  await page.locator('input[type="search"]').fill(term);
  // 300 ms debounce + reload — wait until the spinner (if any) settles.
  await page.waitForTimeout(400);
  await expect(page.locator('.spinner-border')).toHaveCount(0, {
    timeout: 20_000,
  });
}

/** Deletes every grid card matching `term` (used to sweep e2e orphans). */
async function deleteAllMatching(page: Page, term: string): Promise<void> {
  await searchFor(page, term);
  for (let i = 0; i < 20; i++) {
    const cards = itemCards(page);
    const count = await cards.count();
    if (count === 0) {
      return;
    }
    await cards.first().getByTitle('Delete').click();
    const modal = confirmModal(page);
    await expect(modal).toBeVisible();
    await modal.getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(modal).toHaveCount(0, { timeout: 20_000 });
    await expect
      .poll(() => itemCards(page).count(), { timeout: 20_000 })
      .toBeLessThan(count);
  }
}

test.describe('admin media library', () => {
  test.slow();

  test.beforeEach(async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write'], {
      origin: 'http://localhost:4201',
    });
    await loginAsAdmin(page);
  });

  test('upload a PNG, see Unused badge, copy its URL, then delete it', async ({
    page,
  }) => {
    const fileName = `e2e-tiny-${Date.now()}.png`;

    await page.goto('/media');
    await expect(
      page.getByRole('heading', { name: 'Media library' }),
    ).toBeVisible({ timeout: 30_000 });

    // Sweep any orphan uploads left behind by an aborted earlier run.
    await deleteAllMatching(page, 'e2e-tiny');

    // Upload the generated fixture through the (hidden) file input.
    await page.locator('input[type="file"]').setInputFiles({
      name: fileName,
      mimeType: 'image/png',
      buffer: TINY_PNG,
    });

    // Narrow the grid to exactly our upload via the debounced search box
    // (matches the original filename; uniqueness guarantees a single hit).
    await searchFor(page, fileName);
    const card = itemCards(page);
    await expect(card).toHaveCount(1, { timeout: 30_000 });
    await expect(card.locator('.badge', { hasText: 'Unused' })).toBeVisible();

    // Copy URL → success toast (clipboard permissions granted above).
    await card.getByRole('button', { name: 'Copy URL' }).click();
    await expect(
      page.locator('ngb-toast', { hasText: 'URL copied to clipboard.' }),
    ).toBeVisible({ timeout: 20_000 });

    // Delete via the in-app confirm modal.
    await card.getByTitle('Delete').click();
    const modal = confirmModal(page);
    await expect(modal).toBeVisible();
    await modal.getByRole('button', { name: 'Delete', exact: true }).click();

    await expect(
      page.locator('ngb-toast', { hasText: 'File deleted.' }),
    ).toBeVisible({ timeout: 20_000 });
    await expect(itemCards(page)).toHaveCount(0, { timeout: 20_000 });
  });
});
