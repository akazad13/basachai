// Extracted from specs/features/moderation-and-admin.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
// web/e2e/moderation.spec.ts
import type { Page } from '@playwright/test';
import { test, expect } from './fixtures';
import { listing } from './seed-data';

test.beforeEach(async ({ seed, signInAs }) => { await seed('pilot'); await signInAs('moderator'); });
const queue = (page: Page) => page.getByTestId('review-queue').getByRole('article');

test('MOD-TC01 counts and queue order', async ({ page, lang, t, num }) => {
  await page.goto(`/${lang}/admin`);
  await expect(page.getByRole('group', { name: t('s_pending') })).toContainText(num(3));
  await expect(page.getByRole('group', { name: t('s_overdue') })).toContainText(num(2));
  await expect(queue(page)).toHaveCount(3);
  for (const [i, id] of ['P1', 'M4', 'P2'].entries()) await expect(queue(page).nth(i)).toContainText(listing(id).title[lang]);
  await expect(queue(page).nth(0)).toContainText(t('overdue'));
  await expect(queue(page).nth(1)).not.toContainText(t('overdue'));
});

test('MOD-TC02 reject needs a reason', async ({ page, lang, t }) => {
  await page.goto(`/${lang}/admin`);
  await queue(page).nth(2).getByRole('button', { name: t('reject') }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: t('send') }).click();
  await expect(dialog.getByText(t('reason_err'))).toBeVisible();
  await dialog.getByRole('button', { name: t('cancel') }).click();
  await expect(queue(page)).toHaveCount(3);
});

test('MOD-TC03 approve makes it searchable', async ({ page, browser, lang, t }) => {
  await page.goto(`/${lang}/admin`);
  await queue(page).first().getByRole('button', { name: t('approve') }).click();
  await expect(page.getByRole('status')).toHaveText(t('t_approved'));
  await expect(queue(page)).toHaveCount(2);
  const visitor = await (await browser.newContext()).newPage();
  await visitor.goto(`/${lang}/search?area=bazar`);
  await expect(visitor.getByText(listing('P1').title[lang])).toBeVisible();
});

test('MOD-TC04 hide from a report', async ({ page, lang, t }) => {
  await page.goto(`/${lang}/admin`);
  const reports = page.getByTestId('reports-list').getByRole('article');
  await reports.filter({ hasText: listing('Z2').title[lang] }).getByRole('button', { name: t('hide_listing') }).click();
  await expect(reports).toHaveCount(1);
  await page.goto(`/${lang}/search?area=bazar`);
  await expect(page.getByText(listing('Z2').title[lang])).toHaveCount(0);
});

test('MOD-TC05 history shows live edits', async ({ page, lang, t, act, money }) => {
  await act('Z1', 'edit', { monthlyRent: 12500 });
  await page.goto(`/${lang}/admin/listings?q=${encodeURIComponent(listing('Z1').title[lang])}`);
  await page.getByRole('link', { name: t('history') }).first().click();
  const entry = page.getByRole('listitem').filter({ hasText: t('history_edit') }).first();
  await expect(entry).toContainText(money(12_000));
  await expect(entry).toContainText(money(12_500));
});
