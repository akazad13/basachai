// Extracted from specs/journey.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
// web/e2e/journeys/jrn-03-post-and-go-live.spec.ts
import { test, expect } from '../fixtures';
import { actor } from './actors';
import { photos } from '../helpers';

test('JRN-03 @journey an owner posts a first property and it goes live', async ({ browser, page, t, num, money, seed }) => {
  await seed('pilot');
  const title = 'ইসলামপুরে ২ শোবার ঘরের ফ্ল্যাট (JRN-03)';

  // Kamal registers from "Post a property"
  await page.goto('/bn');
  await page.getByRole('button', { name: t('nav_post') }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: t('create_account') }).click();
  await dialog.getByLabel(t('name')).fill('কামাল');
  await dialog.getByLabel(t('phone')).fill('01712000003');
  await dialog.getByLabel(t('password')).fill(process.env.E2E_PASSWORD!);
  await dialog.getByRole('button', { name: t('create_account') }).click();
  await expect(page).toHaveURL(/\/bn\/post/);

  // Steps 1 to 4
  await page.getByLabel(t('l_title')).fill(title);
  await page.getByLabel(t('f_area')).selectOption('islampur');
  await page.getByLabel(t('f_beds')).selectOption('2');
  await page.getByRole('button', { name: t('next') }).click();
  await page.getByLabel(t('l_rent')).fill('9000');
  await page.getByLabel(t('l_service')).fill('500');
  await page.getByRole('button', { name: t('add_cost') }).click();
  await page.getByLabel(t('other_label')).fill('গ্যাস বিল');
  await page.getByLabel(t('other_amount')).fill('1080');
  await page.getByLabel(t('l_advance')).fill('18000');
  await expect(page.getByTestId('cost-total')).toHaveText(money(10_580));
  await page.getByRole('button', { name: t('next') }).click();
  await page.locator('input[type="file"]').setInputFiles(photos(['room-gps.jpg', 'room-2.jpg', 'room-3.jpg']));
  await expect(page.getByText(t('photos_n', { n: num(3) }))).toBeVisible();
  await page.getByRole('button', { name: t('next') }).click();
  await page.getByRole('button', { name: t('submit') }).click();
  await expect(page.getByRole('heading', { name: t('submitted_t') })).toBeVisible();

  // Moderator approves
  const mod = await actor(browser, { role: 'moderator' });
  await mod.goto('/bn/admin');
  const item = mod.getByTestId('review-queue').getByRole('article').filter({ hasText: title });
  await item.getByRole('button', { name: t('approve') }).click();
  await expect(mod.getByRole('status')).toHaveText(t('t_approved'));

  // Kamal sees it live; a tenant finds it
  await page.goto('/bn/dashboard');
  await expect(page.getByRole('article').filter({ hasText: title })).toContainText(t('days_left', { n: num(30) }));
  const tenant = await actor(browser, { mobile: true });
  await tenant.goto('/bn/search?area=islampur');
  await expect(tenant.getByRole('article').filter({ hasText: title })).toContainText(money(10_580));
});
