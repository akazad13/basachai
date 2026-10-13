// Extracted from specs/features/saved-homes.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
// web/e2e/saved.spec.ts
import { test, expect } from './fixtures';
import { signInInDialog } from './helpers';
import { listing } from './seed-data';

test.beforeEach(async ({ seed }) => { await seed('pilot'); });

test('SAVE-TC01 save from a card', async ({ page, lang, t, signInAs }) => {
  await signInAs('tenant');
  await page.goto(`/${lang}/search?area=amin`);
  const card = page.getByRole('article').filter({ hasText: listing('A1').title[lang] });
  await card.getByRole('button', { name: t('save') }).click();
  await expect(card.getByRole('button', { name: t('saved') })).toHaveAttribute('aria-pressed', 'true');
  await page.goto(`/${lang}/saved`);
  await expect(page.getByRole('article')).toHaveCount(1);
  await expect(page.getByRole('article')).toContainText(listing('A1').title[lang]);
});

test('SAVE-TC02 saved homes that stop being live disappear', async ({ page, lang, t, signInAs, act }) => {
  await signInAs('tenant');
  await page.request.put(`${process.env.E2E_API_URL}/v1/me/saved/Z3`);
  await act('Z3', 'mark-rented');
  await page.goto(`/${lang}/saved`);
  await expect(page.getByRole('article')).toHaveCount(0);
  await expect(page.getByText(t('saved_empty'))).toBeVisible();
});

test('SAVE-TC03 save while signed out', async ({ page, lang, t }) => {
  await page.goto(`/${lang}/listing/I1`);
  await page.getByRole('button', { name: t('save') }).click();
  await signInInDialog(page.getByRole('dialog'), 'tenant', t);
  await page.goto(`/${lang}/saved`);
  await expect(page.getByRole('article')).toContainText(listing('I1').title[lang]);
});

test('SAVE-TC04 empty state', async ({ page, lang, t, signInAs }) => {
  await signInAs('tenant');
  await page.goto(`/${lang}/saved`);
  await expect(page.getByText(t('saved_empty'))).toBeVisible();
  await page.getByRole('link', { name: t('nav_find') }).last().click();
  await expect(page).toHaveURL(new RegExp(`/${lang}/search`));
});
