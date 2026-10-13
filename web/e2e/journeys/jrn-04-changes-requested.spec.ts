// Extracted from specs/journey.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
// web/e2e/journeys/jrn-04-changes-requested.spec.ts
import { test, expect } from '../fixtures';
import { actor } from './actors';
import { listing } from '../seed-data';
import { photos } from '../helpers';

test('JRN-04 @journey a listing needs changes before approval', async ({ browser, t, seed }) => {
  await seed('pilot');
  const m4 = listing('M4').title.bn;
  const reason = 'রান্নাঘরের একটি ছবি যোগ করুন।';

  const mod = await actor(browser, { role: 'moderator' });
  await mod.goto('/bn/admin');
  const item = () => mod.getByTestId('review-queue').getByRole('article').filter({ hasText: m4 });
  await item().getByRole('button', { name: t('request_changes') }).click();
  await mod.getByRole('dialog').getByRole('button', { name: t('send') }).click();
  await expect(mod.getByText(t('reason_err'))).toBeVisible();
  await mod.getByRole('dialog').getByRole('textbox').fill(reason);
  await mod.getByRole('dialog').getByRole('button', { name: t('send') }).click();
  await expect(item()).toHaveCount(0);

  const owner = await actor(browser, { role: 'owner' });
  await owner.goto('/bn/dashboard');
  const row = owner.getByRole('article').filter({ hasText: m4 });
  await expect(row).toContainText(t('st_changes'));
  await expect(row).toContainText(reason);
  await row.getByRole('button', { name: t('edit') }).click();
  await owner.getByRole('button', { name: t('next') }).click();
  await owner.getByRole('button', { name: t('next') }).click();
  await owner.locator('input[type="file"]').setInputFiles(photos(['room-4.jpg']));
  await owner.getByRole('button', { name: t('next') }).click();
  await owner.getByRole('button', { name: t('submit') }).click();

  await mod.reload();
  await item().getByRole('button', { name: t('approve') }).click();
  await owner.goto('/bn/dashboard');
  await expect(row).toContainText(t('st_published'));
});
