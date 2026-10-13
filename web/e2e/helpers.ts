import type { Locator, Page } from '@playwright/test';
import { expect } from './fixtures';
import { USERS } from './seed-data';

type T = (key: string, vars?: Record<string, string | number>) => string;

/** Fills the open sign-in dialog with a seeded user's number and the shared test password. */
export async function signInInDialog(dialog: Locator, role: keyof typeof USERS, t: T) {
  await dialog.getByLabel(t('phone')).fill(USERS[role].phone);
  await dialog.getByLabel(t('password')).fill(process.env.E2E_PASSWORD!);
  await dialog.getByRole('button', { name: t('signin_btn'), exact: true }).click();
  await expect(dialog).toBeHidden();
}

/** Fills every required field of step 1 and presses Next. */
export async function fillStep1(page: Page, t: T, overrides: { title?: string; area?: string } = {}) {
  await page.getByLabel(t('l_title')).fill(overrides.title ?? 'Two bedroom test flat');
  await page.getByLabel(t('f_area')).selectOption(overrides.area ?? 'bazar');
  await page.getByRole('button', { name: t('next') }).click();
  await expect(page.locator('[aria-current="step"]')).toContainText(t('step2'));
}

/** Rent 8000, service 0, advance 16000, then Next. */
export async function fillStep2(page: Page, t: T) {
  await page.getByLabel(t('l_rent')).fill('8000');
  await page.getByLabel(t('l_service')).fill('0');
  await page.getByLabel(t('l_advance')).fill('16000');
  await page.getByRole('button', { name: t('next') }).click();
  await expect(page.locator('[aria-current="step"]')).toContainText(t('step3'));
}

export const photos = (names: string[]) => names.map(n => `e2e/files/${n}`);

/** The photo's public id from a delivery URL (Cloudinary, or the local store used in development and test). */
export function publicIdOf(src: string) {
  const local = /\/v1\/dev-photos\/([0-9a-f]{32})/.exec(src);
  if (local) return local[1];
  return src.replace(/^.*\/upload\/[^/]+\/v\d+\//, '').replace(/\.[a-z]+$/, '');
}
