// Extracted from specs/journey.md by scripts/extract-e2e.mjs. Edit the spec, then re-extract.
// web/e2e/journeys/jrn-05-fresh-expire-renew.spec.ts
import { test, expect } from '../fixtures';
import { actor } from './actors';
import { listing } from '../seed-data';

test('JRN-05 @journey a listing expires and is renewed', async ({ browser, t, num, seed, clock }) => {
  await seed('pilot');
  const z1 = listing('Z1').title.bn;
  const owner = await actor(browser, { role: 'owner' });
  const tenant = await actor(browser, { mobile: true });

  await owner.goto('/bn/dashboard');
  await expect(owner.getByTestId('attention-banners')).toContainText(t('al_expiring', { x: z1, n: num(2) }));

  await clock(3);
  await tenant.goto('/bn/search?area=bazar');
  await expect(tenant.getByText(z1)).toHaveCount(0);

  await owner.reload();
  const row = owner.getByRole('article').filter({ hasText: z1 });
  await expect(row).toContainText(t('st_expired'));
  await row.getByRole('button', { name: t('renew') }).click();   // M2 is expired too, so its banner also offers Renew
  await expect(row).toContainText(t('days_left', { n: num(30) }));

  await tenant.reload();
  await expect(tenant.getByText(z1)).toHaveCount(1);
});
