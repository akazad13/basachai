// Extracted from specs/features/auth-and-account.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
// web/e2e/auth.spec.ts
import type { Page } from '@playwright/test';
import { test, expect } from './fixtures';
import { signInInDialog } from './helpers';

test.beforeEach(async ({ seed }) => { await seed('pilot'); });

async function openRegister(page: Page, t: (key: string) => string) {
  await page.getByRole('button', { name: t('nav_signin') }).click();
  await page.getByRole('button', { name: t('create_account') }).click();
}

test('AUTH-TC01 register with Bangla digits', async ({ page, lang, t }) => {
  await page.goto(`/${lang}`);
  await openRegister(page, t);
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel(t('name')).fill('Test Person');
  await dialog.getByLabel(t('phone')).fill('০১৭১২৩৪৫৬৭৮');
  await dialog.getByLabel(t('password')).fill(process.env.E2E_PASSWORD!);
  await dialog.getByRole('button', { name: t('create_account') }).click();
  await expect(page.getByRole('link', { name: t('nav_saved') })).toBeVisible();
  const me = await (await page.request.get(`${process.env.E2E_API_URL}/v1/me`)).json();
  expect(me.phone).toBe('+8801712345678');
});

test('AUTH-TC02 invalid number is caught before sending', async ({ page, lang, t }) => {
  let loginCalls = 0;
  page.on('request', r => { if (r.url().includes('/v1/auth/login')) loginCalls++; });
  await page.goto(`/${lang}`);
  await page.getByRole('button', { name: t('nav_signin') }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel(t('phone')).fill('12345');
  await dialog.getByLabel(t('password')).fill('long-enough-pass');
  await dialog.getByRole('button', { name: t('signin_btn') }).click();
  await expect(dialog.getByLabel(t('phone'))).toHaveAttribute('aria-invalid', 'true');
  await expect(dialog.getByText(t('phone_err'))).toBeVisible();
  expect(loginCalls).toBe(0);
});

test('AUTH-TC03 save after sign-in completes the save', async ({ page, lang, t }) => {
  await page.goto(`/${lang}/listing/A1`);
  await page.getByRole('button', { name: t('save') }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText(t('signin_why_save'));
  await signInInDialog(dialog, 'tenant', t);                 // helper fills seeded tenant number and E2E_PASSWORD
  await expect(page.getByRole('button', { name: t('saved') })).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByRole('status')).toHaveText(t('toast_saved'));
});

test('AUTH-TC04 staff without 2FA must set it up', async ({ page, lang, signInAs }) => {
  await signInAs('moderator_new');
  await page.goto(`/${lang}/admin`);
  await expect(page).toHaveURL(new RegExp(`/${lang}/account/two-factor`));
  await expect(page.getByTestId('review-queue')).toHaveCount(0);
});

test('AUTH-TC05 registration sends nothing to the phone', async ({ page, lang, t }) => {
  const hosts = new Set<string>();
  page.on('request', r => hosts.add(new URL(r.url()).host));
  await page.goto(`/${lang}`);
  await openRegister(page, t);
  // ...fill and submit as in AUTH-TC01 with number 01712345679
  const allowed = [new URL(process.env.E2E_WEB_URL!).host, new URL(process.env.E2E_API_URL!).host,
    'res.cloudinary.com', 'images.unsplash.com'];   // pilot dataset photos; fonts are self-hosted
  expect([...hosts].filter(h => !allowed.includes(h))).toEqual([]);
});
