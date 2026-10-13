// Extracted from specs/journey.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
// web/e2e/journeys/jrn-02-shortlist.spec.ts
import { test, expect } from '../fixtures';
import { listing } from '../seed-data';

test('JRN-02 @journey a tenant shortlists homes and signs up on the way', async ({ page, t, seed }) => {
  await seed('pilot');
  await page.goto('/bn/search?occ=students');
  const card = (id: string) => page.getByRole('article').filter({ hasText: listing(id).title.bn });

  await card('A3').getByRole('button', { name: t('save') }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText(t('signin_why_save'));
  await dialog.getByRole('button', { name: t('create_account') }).click();
  await dialog.getByLabel(t('name')).fill('শিরিন');
  await dialog.getByLabel(t('phone')).fill('০১৭১২৩৪৫৬৭৮');
  await dialog.getByLabel(t('password')).fill(process.env.E2E_PASSWORD!);
  await dialog.getByRole('button', { name: t('create_account') }).click();
  await expect(card('A3').getByRole('button', { name: t('saved') })).toHaveAttribute('aria-pressed', 'true');

  await card('D2').getByRole('button', { name: t('save') }).click();
  await page.getByRole('link', { name: t('nav_saved') }).click();
  await expect(page.getByRole('article')).toHaveCount(2);

  await card('A3').getByRole('button', { name: t('saved') }).click();
  await expect(page.getByRole('article')).toHaveCount(1);
});
