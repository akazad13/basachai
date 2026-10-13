# Content and localization

Bangla is the default language; every string lives in message files, numbers show in the reader's script, and copy is plain, active and specific.

Version 1.0 · 13 October 2026 · Source: Master Spec v1.2, section 13 · Covers FR-19, FR-21, D1

## Languages

- Locales: `bn` (default) and `en`. Routes are prefixed: `/bn/...`, `/en/...`.
- The header toggle shows the other language's name ("English" or "বাংলা") and keeps the user on the same page.
- The choice is stored in the user's profile when signed in, and in a cookie otherwise.
- Reference data (areas, property types, amenities) has `name_bn` and `name_en`. Owner-written text (titles, descriptions) is shown as typed.
- next-intl message files: `messages/bn.json`, `messages/en.json`. No hard-coded UI strings.

## Formats

| Item | Bangla | English | Rule |
| --- | --- | --- | --- |
| Money | ৳১২,৭৮০ | ৳12,780 | Whole taka, lakh grouping; `Intl.NumberFormat('bn-BD')` or `('en-IN')` plus "৳" prefix |
| Large money | ৳২,০০,০০০ | ৳2,00,000 | Lakh grouping in both |
| Counts | ১৫টি বাসা পাওয়া গেছে | 15 homes found | Bangla uses the টি classifier |
| Phone | ০১৭০০-০০০১০১ | 01700-000101 | Stored E.164 `+8801700000101`; `tel:` links use Latin digits |
| Relative time | ৩ দিন আগে, গতকাল | 3 days ago, yesterday | `Intl.RelativeTimeFormat` with `numeric: 'auto'` |
| Dates | ১৫ নভেম্বর | 15 November | Day and month; Asia/Dhaka time zone |
| Floor | ৩ তলা, নিচতলা | Floor 3, Ground floor | 0 = ground floor |
| Studio | স্টুডিও | Studio | Bedrooms = 0 |

### Phone input

Accept `01XXXXXXXXX`, `8801XXXXXXXXX` or `+8801XXXXXXXXX`, with spaces or dashes, in Bangla or Latin digits. Normalise to Latin digits, then to E.164. Valid pattern: `^(?:\+?880|0)1[3-9]\d{8}$`.

## Voice and tone

- Sentence case everywhere. No all-caps labels.
- Plain verbs, active voice: "Show number", "Submit for review", "Mark as rented".
- An action keeps its name through the flow: the "Still available" button leads to "Confirmed. It stays in search for another 30 days."
- Errors say what is wrong and how to fix it, without apologising: "Enter a valid Bangladeshi mobile number, like 01712345678".
- Empty states invite the next action: "You haven't saved any homes yet. Press Save on a home you like."
- Never claim verification (see [trust and safety](../domain/trust-and-safety.md#wording-rules)).

## Core UI copy

| Key | Bangla | English |
| --- | --- | --- |
| nav_find | বাসা খুঁজুন | Find a home |
| nav_post | বিজ্ঞাপন দিন | Post a property |
| nav_dash | আমার বিজ্ঞাপন | My listings |
| nav_saved | সংরক্ষিত | Saved |
| nav_admin | মডারেশন | Moderation |
| nav_signin | সাইন ইন | Sign in |
| total_month | মাসে মোট | Total per month |
| rent | ভাড়া | Rent |
| service | সার্ভিস চার্জ | Service charge |
| advance | অগ্রিম | Advance |
| not_specified | উল্লেখ নেই | Not specified |
| one_time_head | ওঠার সময় একবার দিতে হবে | Paid once when you move in |
| confirmed | {t} নিশ্চিত করা | Confirmed {t} |
| avail_now | এখনই ওঠা যাবে | Available now |
| show_number | নম্বর দেখুন | Show number |
| call | কল করুন | Call |
| not_verified | নম্বর যাচাই করা হয়নি | Number not verified |
| pay_warn | বাসা দেখার আগে কোনো টাকা পাঠাবেন না। | Don't send any money before you visit. |
| report | বিজ্ঞাপন রিপোর্ট করুন | Report this listing |
| save / saved | সংরক্ষণ / সংরক্ষিত | Save / Saved |
| empty_t | এই ফিল্টারে কোনো বাসা নেই | No homes match these filters |
| submit | পর্যালোচনায় জমা দিন | Submit for review |
| reconfirm | এখনও খালি আছে | Still available |
| renew | আবার চালু করুন | Renew |
| pause / resume | সাময়িক বন্ধ / আবার দেখান | Pause / Show again |
| mark_rented | ভাড়া হয়ে গেছে | Mark as rented |

### Additional keys

Keys added for flows the specs and tests define. All of them are now in the prototype's message set.

| Key | Bangla | English |
| --- | --- | --- |
| create_account | নতুন অ্যাকাউন্ট খুলুন | Create an account |
| name | আপনার নাম | Your name |
| continue | চালিয়ে যান | Continue |
| save_changes | পরিবর্তন সংরক্ষণ করুন | Save changes |
| reveal_limited | আপনি সম্প্রতি অনেক নম্বর দেখেছেন। {minutes} মিনিট পরে আবার চেষ্টা করুন। | You have viewed many numbers recently. Try again in {minutes} minutes. |
| photo_too_large | ছবি ১০ এমবি বা তার কম হতে হবে। | Photos must be 10 MB or smaller. |
| photo_type | JPEG, PNG বা WebP ছবি দিন। | Use a JPEG, PNG or WebP photo. |
| history | ইতিহাস | History |
| history_edit | প্রকাশিত অবস্থায় সম্পাদনা | Edited while live |
| issue_reset | পাসওয়ার্ড রিসেট লিংক দিন | Issue password reset |
| reset_link | রিসেট লিংক | Reset link |
| new_password | নতুন পাসওয়ার্ড | New password |
| set_password | পাসওয়ার্ড সেট করুন | Set password |
| reset_used | এই লিংকের মেয়াদ শেষ বা এটি আগেই ব্যবহার করা হয়েছে। | This link has expired or was already used. |
| phone_taken | এই নম্বরে আগেই অ্যাকাউন্ট আছে। সাইন ইন করুন। | This number already has an account. Sign in instead. |
| t_saved_live | পরিবর্তন সংরক্ষণ হয়েছে। বিজ্ঞাপনটি প্রকাশিত আছে। | Changes saved. The listing is still live. |

### Status names

| Status | Bangla | English |
| --- | --- | --- |
| `draft` | খসড়া | Draft |
| `published` | প্রকাশিত | Live |
| `pending` | পর্যালোচনায় | In review |
| `changes` | পরিবর্তন দরকার | Changes needed |
| `rejected` | বাতিল | Rejected |
| `paused` | বন্ধ | Paused |
| `rented` | ভাড়া হয়েছে | Rented |
| `expired` | মেয়াদ শেষ | Expired |
| `archived` | আর্কাইভ | Archived |

The full message set is in the [prototype](../../BasaChai-Prototype.html) source (`T.bn`, `T.en`) and moves to the message files as-is.

## SEO

| Page | URL | Rendering |
| --- | --- | --- |
| Home | `/bn` | SSR, cached 60 s at the edge, purged on change |
| Area results | `/bn/rent/dhaka/dhamrai/{area}` | SSR, cached 60 s at the edge, purged on change; filters continue on `/search` |
| Property | `/bn/rent/dhaka/dhamrai/{area}/{slug}-{shortId}` (and `/bn/listing/{shortId}`) | SSR, cached 60 s, purged on change |
| Search | `/bn/search?...` | SSR on every request (any filter combination); not cached |
| Owner or staff preview | `/bn/listing/{shortId}/preview` | SSR with the visitor's session, never cached, `noindex`; admin pages link here |
| Static pages | `/bn/about`, `/bn/how-it-works`, `/bn/safety`, `/bn/support`, `/bn/terms`, `/bn/privacy` | Static; never purged |

Caching uses Next.js incremental regeneration on Netlify's edge (`Cache-Control: s-maxage=60`). After any change a visitor could see (a Live listing's edit, photos or status, an approval, an owner's name, areas, types, amenities or the home cost example) the API asks the web app's `/api/revalidate` (signed with a shared secret) to drop the affected pages at once: the listing's page and the home and area lists, or every page built from API data (static pages are not touched). Drafts that were never public purge nothing. Purges inside a transaction are sent after it commits, and in production they go through a queue with one retry so no save waits on the web host. A page whose data cannot be loaded is not cached: the last good page stays. Changes no save triggers (a listing passing its expiry time) show within a few minutes: a page is refreshed in the background after 60 seconds, and after 5 minutes without visits it is rendered fresh instead of served stale. An area URL with filters in its query continues on `/search`. Cloudflare does not cache HTML (no cache rule), so the purge reaches the only cache.

- Every property page: title, meta description, canonical URL, `hreflang` for bn and en, Open Graph image (cover photo).
- Sitemap of live listings and area pages, regenerated hourly.
- Unavailable listings: `noindex` and removed from the sitemap.

## Acceptance criteria

- **L10N-1** Given Bangla mode, then all money, counts, dates and phone numbers use Bangla digits.
- **L10N-2** Given English mode, then the same values use Latin digits with lakh grouping (৳2,00,000).
- **L10N-3** Given the toggle on a property page, then the same listing opens in the other language.
- **L10N-4** Given a property page, then it has a canonical URL and `hreflang` links for both languages.
- **L10N-5** Given a code search, then no UI string is hard-coded outside the message files.
- **L10N-6** Given a purge of every page, then the static, home, area and property pages still load.

## Test cases

| ID | Covers | Case | Expected |
| --- | --- | --- | --- |
| L10N-TC01 | L10N-1 | Bangla: open search and D3's page | Count, money, bedrooms and revealed phone use only Bangla digits (no `[0-9]` inside `main` except in URLs) |
| L10N-TC02 | L10N-2 | English: set A2's advance to 2,00,000 through the test hook; open A2 | Advance shows `৳2,00,000`; no Bangla digits inside `main` except owner-written text |
| L10N-TC03 | L10N-3, L10N-4 | Open D3 in Bangla; press "English" | Same listing in English; page has a canonical link and `hreflang` links for `bn` and `en` |
| L10N-TC04 | L10N-6 | Seed (which purges every page); open each static page, home, an area page and D3 | Each answers 200 with its heading |
| L10N-CI01 | L10N-5 | CI lint | `eslint-plugin-i18next` rule `no-literal-string` passes on `web/app` and `web/components` |

### Automation

```ts
// web/e2e/l10n.spec.ts
import { test, expect } from './fixtures';

test.beforeEach(async ({ seed }) => { await seed('pilot'); });

test('L10N-TC01 Bangla digits everywhere in Bangla mode', async ({ page, t }, info) => {
  test.skip(!info.project.name.startsWith('bn'));
  for (const path of ['/bn/search', '/bn/listing/D3']) {
    await page.goto(path);
    if (path.includes('listing')) await page.getByRole('button', { name: t('show_number') }).click();
    expect(await page.locator('main').innerText(), path).not.toMatch(/[0-9]/);
  }
});

test('L10N-TC02 Latin digits and lakh grouping in English', async ({ page, act }, info) => {
  test.skip(info.project.name !== 'en-desktop');
  await act('A2', 'edit', { advance: { amount: 200000, notSpecified: false } });
  await page.goto('/en/listing/A2');
  await expect(page.getByTestId('cost-breakdown')).toContainText('৳2,00,000');
});

test('L10N-TC03 toggle keeps the listing; canonical and hreflang', async ({ page }, info) => {
  test.skip(info.project.name !== 'bn-desktop');
  await page.goto('/bn/listing/D3');
  await page.getByRole('button', { name: 'English' }).click();
  await expect(page).toHaveURL(/\/en\/.*D3/);
  await expect(page.locator('link[rel="canonical"]')).toHaveAttribute('href', /\/en\/rent\/dhaka\/dhamrai\/dokkhin\/.+-D3$/);
  await expect(page.locator('link[rel="alternate"][hreflang="bn"]')).toHaveCount(1);
  await expect(page.locator('link[rel="alternate"][hreflang="en"]')).toHaveCount(1);
});

test('L10N-TC04 pages still load after every page was purged', async ({ page }, info) => {
  const lang = info.project.name.slice(0, 2);
  const paths = ['about', 'how-it-works', 'safety', 'support', 'terms', 'privacy'].map(p => `/${lang}/${p}`)
    .concat([`/${lang}`, `/${lang}/rent/dhaka/dhamrai/dokkhin`, `/${lang}/listing/D3`]);
  for (const path of paths) {
    const response = await page.goto(path);
    expect(response?.status(), path).toBe(200);
    await expect(page.locator('main h1').first(), path).toBeVisible();
  }
});
```

```jsonc
// web/.eslintrc.json (excerpt) - L10N-CI01
{
  "plugins": ["i18next"],
  "overrides": [{ "files": ["app/**/*.tsx", "components/**/*.tsx"],
                  "rules": { "i18next/no-literal-string": ["error", { "mode": "jsx-text-only" }] } }]
}
```

## Related

[design system](design-system.md) · [auth and account](../features/auth-and-account.md) · [search](../features/search.md)
