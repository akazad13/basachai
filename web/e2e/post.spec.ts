// Extracted from specs/features/post-property.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
// web/e2e/post.spec.ts
import { test, expect } from './fixtures';
import { fillStep1, fillStep2, publicIdOf } from './helpers';
import { listing } from './seed-data';

test.beforeEach(async ({ seed, signInAs }) => { await seed('pilot'); await signInAs('owner'); });

test('POST-TC01 step 1 validation', async ({ page, lang, t }) => {
  await page.goto(`/${lang}/post`);
  await page.getByRole('button', { name: t('next') }).click();
  await expect(page.getByLabel(t('l_title'))).toHaveAttribute('aria-invalid', 'true');
  await expect(page.getByLabel(t('f_area'))).toHaveAttribute('aria-invalid', 'true');
  await expect(page.getByLabel(t('l_title'))).toBeFocused();
  await expect(page.locator('[aria-current="step"]')).toContainText(t('step1'));
});

test('POST-TC02 live cost preview', async ({ page, lang, t, money }, info) => {
  test.skip(info.project.name === 'bn-mobile');              // preview panel is desktop only
  await page.goto(`/${lang}/post`);
  await fillStep1(page, t, { title: 'Test flat in Islampur', area: 'islampur' });
  await page.getByLabel(t('l_rent')).fill('9000');
  await page.getByLabel(t('l_service')).fill('500');
  await page.getByRole('button', { name: t('add_cost') }).click();
  await page.getByLabel(t('other_label')).fill('Gas bill');
  await page.getByLabel(t('other_amount')).fill('1080');
  await expect(page.getByTestId('cost-total')).toHaveText(money(10_580));
  await page.getByLabel(t('l_adv_none')).check();
  await expect(page.getByLabel(t('l_advance'))).toBeDisabled();
  await expect(page.getByTestId('cost-breakdown')).toContainText(t('not_specified'));
});

test('POST-TC03 photo minimum and size limit', async ({ page, lang, t, num }) => {
  await page.goto(`/${lang}/post`);
  await fillStep1(page, t); await fillStep2(page, t);
  const input = page.locator('input[type="file"]');
  await input.setInputFiles(['e2e/files/room-1.jpg', 'e2e/files/room-2.jpg']);
  await expect(page.getByText(t('photos_n', { n: num(2) }))).toBeVisible();
  await expect(page.getByText(t('need_photos', { n: num(1) }))).toBeVisible();
  await input.setInputFiles({ name: 'big.jpg', mimeType: 'image/jpeg', buffer: Buffer.alloc(12 * 1024 * 1024, 0xff) });
  await expect(page.getByText(t('photo_too_large'))).toBeVisible();
  await expect(page.getByTestId('photo-item')).toHaveCount(2);
});

test('POST-TC04 reorder sets the cover', async ({ page, lang, t }) => {
  await page.goto(`/${lang}/post`);
  await fillStep1(page, t); await fillStep2(page, t);
  await page.locator('input[type="file"]').setInputFiles(['e2e/files/room-1.jpg', 'e2e/files/room-2.jpg', 'e2e/files/room-3.jpg']);
  const third = page.getByTestId('photo-item').nth(2);
  const src = await third.locator('img').getAttribute('src');
  await third.getByRole('button', { name: t('move_left') }).click();
  await page.getByTestId('photo-item').nth(1).getByRole('button', { name: t('move_left') }).click();
  await expect(page.getByTestId('photo-item').first()).toContainText(t('cover'));
  await page.getByRole('button', { name: t('next') }).click();
  await expect(page.getByRole('article').locator('img')).toHaveAttribute('src', new RegExp(publicIdOf(src!)));
});

test('POST-TC05 submit puts the listing in review', async ({ page, lang, t }) => {
  await page.goto(`/${lang}/post`);
  await fillStep1(page, t, { title: 'Submit test flat' }); await fillStep2(page, t);
  await page.locator('input[type="file"]').setInputFiles(['e2e/files/room-1.jpg', 'e2e/files/room-2.jpg', 'e2e/files/room-3.jpg']);
  await page.getByRole('button', { name: t('next') }).click();
  await page.getByRole('button', { name: t('submit') }).click();
  await expect(page.getByRole('heading', { name: t('submitted_t') })).toBeVisible();
  await page.goto(`/${lang}/dashboard`);
  const row = page.getByRole('article').filter({ hasText: 'Submit test flat' });
  await expect(row).toContainText(t('st_pending'));
});

test('POST-TC06 drafts survive leaving the page', async ({ page, lang, t }) => {
  await page.goto(`/${lang}/post`);
  await fillStep1(page, t, { title: 'Draft test flat' });
  await page.getByLabel(t('l_rent')).fill('7000');
  await page.getByRole('button', { name: t('next') }).click();          // saves step 2 (fails validation, still saved)
  await page.goto(`/${lang}/dashboard`);
  await page.getByRole('article').filter({ hasText: 'Draft test flat' }).getByRole('button', { name: t('continue') }).click();
  await expect(page.getByLabel(t('l_rent'))).toHaveValue('7000');         // reopens where the owner left off
  await page.getByRole('button', { name: t('prev') }).click();
  await expect(page.getByLabel(t('l_title'))).toHaveValue('Draft test flat');
});

test('POST-TC07 editing a live listing keeps it live and is audited', async ({ page, lang, t, api }) => {
  await page.goto(`/${lang}/dashboard`);
  const row = () => page.getByRole('article').filter({ hasText: listing('Z1').title[lang] });
  await row().getByRole('button', { name: t('edit') }).click();
  await page.getByRole('button', { name: t('next') }).click();
  await page.getByLabel(t('l_rent')).fill('12500');
  await page.getByRole('button', { name: t('save_changes') }).click();
  await expect(page.getByRole('status')).toHaveText(t('t_saved_live'));
  const audit = await (await api.get('/v1/test/audit?entity=listing&id=Z1')).json();
  expect(audit[0].before.monthlyRent).toBe(12000);
  expect(audit[0].after.monthlyRent).toBe(12500);
  await page.goto(`/${lang}/dashboard`);
  await expect(row()).toContainText(t('st_published'));
});
