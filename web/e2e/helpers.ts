import { expect, type BrowserContext, type Page } from '@playwright/test';

export const ADMIN_EMAIL = 'admin@mystore.local';
export const ADMIN_PASSWORD = 'Admin@123';

/**
 * Language is persisted in the `atb_lang` cookie (see core LanguageService).
 * Set it explicitly before first navigation so a previous test/run can never
 * leak Arabic (or English) into a spec that assumes the other.
 */
export async function setLangCookie(
  context: BrowserContext,
  lang: 'en' | 'ar',
  url: string,
): Promise<void> {
  await context.addCookies([{ name: 'atb_lang', value: lang, url }]);
}

/**
 * Signs into the admin console (:4201) and waits for the dashboard heading.
 * Forces the EN language cookie first so assertions on English copy hold.
 */
export async function loginAsAdmin(page: Page): Promise<void> {
  await setLangCookie(page.context(), 'en', 'http://localhost:4201');
  await page.goto('/login');
  await page.locator('#email').fill(ADMIN_EMAIL);
  await page.locator('#password').fill(ADMIN_PASSWORD);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  // Dev-server lazy routes can take a while on first compile.
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible({
    timeout: 30_000,
  });
}

/** Asserts the document has no horizontal scrollbar (mobile/tablet layouts). */
export async function expectNoBodyHScroll(page: Page): Promise<void> {
  await expect
    .poll(
      () =>
        page.evaluate(() => {
          const el = document.documentElement;
          return el.scrollWidth - el.clientWidth;
        }),
      {
        message: `documentElement.scrollWidth must not exceed clientWidth on ${page.url()}`,
        timeout: 10_000,
      },
    )
    .toBeLessThanOrEqual(0);
}

/** Parses a MoneyPipe rendering ("JOD 1,013.000") into a number. */
export function parseMoney(text: string): number {
  const cleaned = text.replace(/[^0-9.]/g, '');
  const value = Number(cleaned);
  if (!Number.isFinite(value)) {
    throw new Error(`Could not parse money value from "${text}"`);
  }
  return value;
}

/** The ng-bootstrap confirm modal (in-app replacement for window.confirm). */
export function confirmModal(page: Page) {
  return page.locator('ngb-modal-window.modal.show');
}

/**
 * Registers a listener that records (and dismisses) any NATIVE browser dialog.
 * Returns an accessor for the fired dialogs — should stay empty now that the
 * app uses its own in-app confirm modal.
 */
export function trackNativeDialogs(page: Page): () => string[] {
  const fired: string[] = [];
  page.on('dialog', (dialog) => {
    fired.push(`${dialog.type()}: ${dialog.message()}`);
    void dialog.dismiss().catch(() => undefined);
  });
  return () => fired;
}
