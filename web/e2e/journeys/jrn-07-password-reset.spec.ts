// Extracted from specs/journey.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
// web/e2e/journeys/jrn-07-password-reset.spec.ts
import { test, expect } from '../fixtures';
import { actor } from './actors';
import { USERS } from '../seed-data';

test('JRN-07 @journey an owner forgets their password', async ({ browser, t, seed }) => {
  await seed('pilot');
  const oldPhone = await actor(browser, { role: 'owner' });               // existing session
  const admin = await actor(browser, { role: 'admin' });

  await admin.goto(`/bn/admin/users?q=${USERS.owner.phone}`);
  await admin.getByRole('button', { name: t('issue_reset') }).click();
  const link = await admin.getByRole('textbox', { name: t('reset_link') }).inputValue();

  const newPhone = await actor(browser, { mobile: true });
  await newPhone.goto(link);
  await newPhone.getByLabel(t('new_password')).fill('another-long-password');
  await newPhone.getByRole('button', { name: t('set_password') }).click();
  await expect(newPhone.getByRole('link', { name: t('nav_dash') })).toBeVisible();

  await oldPhone.goto('/bn/dashboard');
  await expect(oldPhone.getByRole('dialog', { name: t('signin_t') })).toBeVisible();   // old session ended

  await newPhone.goto(link);
  await expect(newPhone.getByText(t('reset_used'))).toBeVisible();
});
