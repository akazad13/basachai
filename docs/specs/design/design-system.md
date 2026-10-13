# Design system

A calm river-green palette with one brass accent, rounded Baloo Da 2 headings, and a light theme only. The cost breakdown is the signature element; everything else stays quiet. The [prototype](../../BasaChai-Prototype.html) is the visual reference.

Version 1.0 · 13 October 2026 · Source: Master Spec v1.2, section 12 · Covers D18, D19, D20

## Colour tokens

| Token | Hex | Use |
| --- | --- | --- |
| `--paper` | `#F4F6F2` | Page background |
| `--surface` | `#FFFFFF` | Cards, panels, inputs |
| `--ink` | `#17231F` | Text |
| `--muted` | `#586861` | Secondary text |
| `--line` | `#D8E0DA` | Borders, dotted leaders |
| `--green` | `#0F5E50` | Primary buttons, links, Live status |
| `--green-tint` | `#E2EEEA` | Active nav, chips, Live badge background |
| `--on-green` | `#FFFFFF` | Text on green |
| `--brass` | `#B7862A` | Total rule, focus outline, Submit for review |
| `--brass-tint` | `#F5EBD5` | One-time cost box, In review badge, reminder banners |
| `--red` | `#A63D2A` | Errors, Expired, Changes needed, Overdue |
| `--red-tint` | `#F6E3DE` | Error and expired banners |

- **Light theme only** (D19): declare `color-scheme: light` and `<meta name="color-scheme" content="light only">`. Do not ship dark-mode styles.
- Body text on paper and surface meets WCAG AA (4.5:1). Muted text is for secondary information only.

## Typography

| Role | Font | Size and weight | Line height |
| --- | --- | --- | --- |
| Headings, logo, money | Baloo Da 2 | H1 30 px (40 px from 900 px), H2 22 to 24 px, H3 18 px; 600 to 700 | 1.25 |
| Bangla body | Noto Sans Bengali | 16 px, 400 / 600 | 1.75 |
| English body | Nunito, falling back to Noto Sans Bengali for Bangla and ৳ | 16 px, 400 / 600 | 1.55 |
| Small text | Body font | 13 to 14 px | 1.5 |
| Total per month | Baloo Da 2 | 24 px on cards, 28 to 30 px in the breakdown, 700 | 1.2 |

- Load from Google Fonts with `display=swap`: Baloo Da 2 (500 to 800), Noto Sans Bengali (400 to 700), Nunito (400 to 700). Self-host before launch for speed.
- Switch the body font by `html[lang]`: `bn` uses Noto Sans Bengali; `en` uses Nunito.
- Numbers use the reader's script (Bangla digits in Bangla mode).

## Spacing, radius, elevation

| Item | Value |
| --- | --- |
| Spacing scale | 4, 8, 12, 16, 20, 24, 32, 40, 56 px |
| Radius | Cards and panels 14 px; inputs and buttons 10 px; small buttons 8 px; chips, badges and pills fully rounded |
| Borders | 1 px `--line` |
| Shadows | None, except the mobile filter sheet |
| Focus | 3 px `--brass` outline, 2 px offset, on every interactive element |

## Components

### Cost breakdown

See [listing and pricing](../domain/listing-and-pricing.md#display). Dotted leader per line, brass 4 px double rule above the total, one-time box in brass tint.

### Listing card

| Part | Rule |
| --- | --- |
| Photo | 4:3, `object-fit: cover`; drawn room placeholder underneath while loading or offline; "Photo 1 of N" tag |
| Freshness | 8 px dot (green within 7 days, brass after) and "Confirmed {relative time}" |
| Save | Pill button, 13 px; brass when saved |
| Cost | Brass double rule, total large, split and advance small |
| Layout | Stacked under 640 px; photo left (260 px) from 640 px in result lists |

### Status badges

| Status | Style |
| --- | --- |
| Live | Green tint, green text |
| In review | Brass tint, ink text |
| Changes needed, Rejected, Expired | Red tint, red text |
| Paused, Rented, Archived | Paper, muted text, line border |
| Owner | Green tint |
| Number not verified | Paper, muted text, line border |

### Buttons

| Type | Use |
| --- | --- |
| Primary (green) | Search homes, Show number, Next, Approve, Still available, Renew |
| Brass | Submit for review |
| Ghost (outlined) | Secondary actions, Back, Pause |
| Danger (red outline) | Reject, Hide listing |
| Link | Report this listing, Clear all filters |

Labels say exactly what happens. An action keeps its name through the flow.

### Other components

Filter chips with a 26 px round remove button; segmented pills for Now / Date; stepper with numbered circles; banners (brass for reminders, red for problems); dialogs (480 px max, 16 px radius, focus moves to the first field, Escape closes); toast messages bottom-centre for 3.2 seconds, above the tab bar on phones, announced with `role="status"`.

## Layout and breakpoints

| Width | Change |
| --- | --- |
| 600 px | Home recent homes become 2 x 2 |
| 640 px | Result cards go horizontal; home search form fields in one row |
| 760 px | Below: header links become a bottom tab bar (64 px) |
| 860 px | Below: filters become a slide-up sheet |
| 900 px | Property page and home hero become two columns |
| 960 px | Post a property shows the sticky cost preview |
| 1,100 px | Home recent homes become 4 across |

Content width up to 1,180 px with 16 px side padding. No horizontal scrolling at any width from 360 px.

## Photos

- Real photos of Bangladeshi homes; demo photos must look local (concrete blocks with grilled balconies, tin-roof homes, ceiling fans, steel-utensil kitchens).
- No landmarks, no recognisable faces.
- Demo photos are credited in the footer as samples that do not show the actual listed homes.

## Accessibility

- WCAG 2.2 AA.
- Every input has a visible label; errors use `aria-invalid` and `aria-describedby`.
- Icon-only buttons have `aria-label` (for example "Remove 2 bedrooms filter").
- Toggle buttons use `aria-pressed`; the current page link uses `aria-current="page"`; the current step uses `aria-current="step"`.
- Respect `prefers-reduced-motion`: no transitions.

## Acceptance criteria

- **DS-1** Given a device set to dark mode, then every page renders in the light theme, including form controls.
- **DS-2** Given Bangla mode, then body text renders in Noto Sans Bengali and headings and totals in Baloo Da 2.
- **DS-3** Given English mode, then body text renders in Nunito and ৳ still renders correctly.
- **DS-4** Given keyboard-only use, then every interactive element shows the brass focus outline.
- **DS-5** Given widths 360, 768 and 1,280 px, then no page scrolls horizontally.

## Test cases

| ID | Covers | Case | Expected |
| --- | --- | --- | --- |
| DS-TC01 | DS-1 | Emulate `prefers-color-scheme: dark`; open home, search, property, dashboard | Body background `rgb(244, 246, 242)`; inputs have a white background |
| DS-TC02 | DS-2, DS-3 | Open the property page in each language | Bangla: body font starts with "Noto Sans Bengali"; English: body font starts with "Nunito"; H1 and the total use "Baloo Da 2"; ৳ renders (glyph width above 0) |
| DS-TC03 | DS-4 | Tab through the home page header, search form and first card | Every focused element shows a 3 px outline in `rgb(183, 134, 42)` |
| DS-TC04 | DS-5, WCAG | For widths 360, 768, 1,280 px and every route: no horizontal scroll, and axe finds no violations | `scrollWidth <= innerWidth`; axe `violations` is empty |

### Automation

```ts
// web/e2e/design.spec.ts
import AxeBuilder from '@axe-core/playwright';
import { test, expect } from './fixtures';

const ROUTES = ['', '/search', '/listing/D3', '/saved', '/dashboard', '/post', '/admin'];
test.beforeEach(async ({ seed }) => { await seed('pilot'); });

test('DS-TC01 light theme even in dark mode', async ({ page, lang }) => {
  await page.emulateMedia({ colorScheme: 'dark' });
  for (const path of ['', '/search', '/listing/D3']) {
    await page.goto(`/${lang}${path}`);
    expect(await page.evaluate(() => getComputedStyle(document.body).backgroundColor)).toBe('rgb(244, 246, 242)');
  }
});

test('DS-TC02 fonts per language', async ({ page, lang }) => {
  await page.goto(`/${lang}/listing/D3`);
  await page.evaluate(() => document.fonts.ready);
  const body = await page.evaluate(() => getComputedStyle(document.body).fontFamily);
  expect(body).toMatch(lang === 'bn' ? /^"?Noto Sans Bengali/ : /^"?Nunito/);
  expect(await page.getByRole('heading', { level: 1 }).evaluate(el => getComputedStyle(el).fontFamily)).toMatch(/^"?Baloo Da 2/);
  expect(await page.getByTestId('cost-total').evaluate(el => getComputedStyle(el).fontFamily)).toMatch(/^"?Baloo Da 2/);
});

test('DS-TC03 visible focus outline', async ({ page, lang }) => {
  await page.goto(`/${lang}`);
  for (let i = 0; i < 12; i++) {
    await page.keyboard.press('Tab');
    const outline = await page.evaluate(() => {
      const s = getComputedStyle(document.activeElement!);
      return `${s.outlineWidth} ${s.outlineStyle} ${s.outlineColor}`;
    });
    expect(outline).toBe('3px solid rgb(183, 134, 42)');
  }
});

for (const width of [360, 768, 1280]) {
  test(`DS-TC04 no sideways scroll and no axe violations at ${width}px`, async ({ page, lang, signInAs }) => {
    await signInAs('admin');                                  // admin can open every route
    await page.setViewportSize({ width, height: 900 });
    for (const path of ROUTES) {
      await page.goto(`/${lang}${path}`);
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), path).toBe(true);
      const { violations } = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag22aa']).analyze();
      expect(violations, path).toEqual([]);
    }
  });
}
```

## Related

[content and localization](content-and-localization.md) · [listing and pricing](../domain/listing-and-pricing.md) · [Bangla font specimens](../../BasaChai-Bangla-Fonts.html)
