# Moderation and admin

Moderators review every new listing and every report within one business day; admins also manage users and configuration. Every action is audited.

Version 1.0 · 13 October 2026 · Route `/admin`, moderators and admins · Covers FR-12, FR-15, FR-17, FR-18

## User stories

- As a moderator, I review new listings oldest first and approve, reject or request changes with a reason the owner sees.
- As a moderator, I handle reports by hiding a listing or dismissing the report.
- As a moderator, I can see a listing's full history, including edits made while Live.
- As an admin, I can restrict or suspend users, issue password resets, and manage areas, property types and amenities.

## Layout

### Summary counts

| Count | Rule |
| --- | --- |
| Waiting for review | Listings In review |
| Live listings | Live, unhidden, unexpired |
| Open reports | Reports not resolved |
| Older than one business day | Queue items and open reports older than 24 hours; shown red when above 0 |

### Review queue

Sorted oldest first. Each item shows:

- Submitted time ("Submitted 5 hours ago") and an "Overdue" tag after 24 hours
- Title, area, owner name
- Up to 4 photo thumbnails and the photo count
- Description
- Cost breakdown
- Actions: **Approve**, **Request changes**, **Reject**

Request changes and Reject open a dialog: "What needs to change?" or "Why are you rejecting this?", a required reason ("A reason is required"), and Send.

### Reports

Each report shows the reason badge (red for suspicious), age and Overdue tag, the listing title (link), the reporter's note, and actions **Hide listing** and **Dismiss**.

### Admin-only areas

| Area | Capabilities |
| --- | --- |
| Listings search | By title, area, owner, status; open full history |
| Users | Search by name or number; Restrict (cannot post), Suspend (cannot sign in), Reinstate, Issue password reset |
| Configuration | Areas and their aliases, property types, amenities (Bangla and English names, active flag) |
| Audit | Filter by actor, action, listing or user, and date |

## Behaviour

- **Approve:** status Live, `last_confirmed_at` and `expires_at` set (now + 30 days). Message "Approved. The listing is now live."
- **Request changes:** status Changes needed with reason. Message "The owner has been asked for changes."
- **Reject:** status Rejected with reason. Message "Listing rejected."
- **Hide listing** (from a report): sets the hide flag on the listing and resolves every open report on that listing. Message "The listing is hidden from search." An Unhide action is available on the listing.
- **Dismiss:** resolves the report with no change. Message "Report dismissed."
- Every action writes `moderation_actions` and `audit_log` in the same transaction as the change.
- **History view** lists status changes, moderator actions and Live edits (before and after values) in time order. This is the check on D23.

## States

| State | Behaviour |
| --- | --- |
| Queue empty | "Nothing left to review." |
| No open reports | "No open reports." |
| Two moderators act on one item | The second gets "This listing was already handled" and the queue refreshes |

## Data and API

`/v1/admin/stats`, `/v1/admin/listings`, `/v1/admin/listings/{id}/history`, `/v1/admin/listings/{id}/{approve|reject|request-changes|hide|unhide}`, `/v1/admin/reports`, `/v1/admin/reports/{id}/resolve`, `/v1/admin/users`, `/v1/admin/users/{id}/{restrict|suspend|reinstate|issue-password-reset}`, `/v1/admin/audit`, configuration CRUD. See [API](../tech/api.md#admin).

## Analytics

`listing_published` on approve.

## Acceptance criteria

- **MOD-1** Given three listings submitted 30, 5 and 3 hours ago, then the queue lists them oldest first and the 30-hour one is tagged Overdue.
- **MOD-2** Given I press Reject without a reason, then "A reason is required" shows and nothing changes.
- **MOD-3** Given I approve a listing, then it appears in search immediately with 30 days left.
- **MOD-4** Given a suspicious report, when I press Hide listing, then the listing leaves search and the report closes.
- **MOD-5** Given an owner edited a Live listing's rent, then its history shows the old and new rent with time and actor.
- **MOD-6** Given a moderator (not admin), then user and configuration areas are not visible and their endpoints return 403.
- **MOD-7** Given any moderator action, then an audit log entry exists with actor, action, target and time.

## Out of scope

Automated duplicate detection, bulk actions, moderator performance reports.

## Test cases

Dataset `pilot`; signed in as `moderator` unless noted. Queue: P1 (30 h, overdue), M4 (5 h), P2 (3 h). Reports: R1 on Z2 (suspicious, 30 h), R2 on I2 (4 h).

### E2E test cases

| ID | Covers | Preconditions | Steps | Expected |
| --- | --- | --- | --- | --- |
| MOD-TC01 | MOD-1 | Seed `pilot` | 1. Open `/{lang}/admin` | Counts: 3 waiting, 15 live, 2 open reports, 2 overdue (red). Queue order P1, M4, P2; only P1 tagged Overdue |
| MOD-TC02 | MOD-2 | Seed `pilot` | 1. Press "Reject" on P2<br>2. Press "Send" with an empty reason | "A reason is required" shows; P2 stays in the queue |
| MOD-TC03 | MOD-3 | Seed `pilot` | 1. Press "Approve" on P1<br>2. Open `/{lang}/search?area=bazar` as a visitor | Approved message; queue shows 2; P1 appears in Dhamrai Bazar results, confirmed today |
| MOD-TC04 | MOD-4 | Seed `pilot` | 1. In Reports, press "Hide listing" on R1<br>2. Open `/{lang}/search?area=bazar` | Hidden message; reports list shows 1; Z2 absent from search |
| MOD-TC05 | MOD-5 | Seed `pilot`; owner changed Z1 rent from 12,000 to 12,500 through the test hook | 1. Search Z1 in admin listings<br>2. Open its history | History shows the edit with rent 12,000 and 12,500, the owner as actor, and the time |

### Automation (E2E)

```ts
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
```

The `act` fixture accepts an optional third argument with field changes for the `edit` action.

### API test cases

| ID | Covers | Case | Expected |
| --- | --- | --- | --- |
| MOD-API01 | MOD-6 | Moderator calls `/admin/users` and creates an amenity | Both 403 |
| MOD-API02 | MOD-7 | Moderator approves P1 | Status `published`, expiry now + 30 days, moderation action and audit entry stored |
| MOD-API03 | Concurrency | Moderator approves P2; admin then rejects P2 | Second call 409 `already_handled` |

### Automation (API)

```csharp
// tests/BasaChai.Api.Tests/ModerationTests.cs
public class ModerationTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => f.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact] // MOD-API01: MOD-6
    public async Task Moderators_cannot_reach_admin_only_endpoints()
    {
        var mod = await f.AsAsync("moderator");
        Assert.Equal(HttpStatusCode.Forbidden, (await mod.GetAsync("/v1/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mod.PostAsJsonAsync("/v1/admin/amenities", new { Key = "x" })).StatusCode);
    }

    [Fact] // MOD-API02: MOD-7
    public async Task Approve_writes_moderation_action_audit_and_expiry()
    {
        var mod = await f.AsAsync("moderator");
        (await mod.PostAsync("/v1/admin/listings/P1/approve", null)).EnsureSuccessStatusCode();
        await using var db = f.Db();
        var p1 = await db.Listings.SingleAsync(l => l.ShortId == "P1");
        Assert.Equal(ListingStatus.Published, p1.Status);
        Assert.Equal(f.Clock.GetUtcNow().AddDays(30), p1.ExpiresAt);
        Assert.True(await db.ModerationActions.AnyAsync(a => a.ListingId == p1.Id && a.Action == "approve"));
        Assert.True(await db.AuditLog.AnyAsync(a => a.EntityId == p1.Id.ToString() && a.Action == "listing.approve"));
    }

    [Fact] // MOD-API03: concurrent moderators
    public async Task Second_moderator_gets_already_handled()
    {
        var a = await f.AsAsync("moderator");
        var b = await f.AsAsync("admin");
        (await a.PostAsync("/v1/admin/listings/P2/approve", null)).EnsureSuccessStatusCode();
        var res = await b.PostAsJsonAsync("/v1/admin/listings/P2/reject", new { Reason = "Late reject" });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("already_handled", (await res.Content.ReadFromJsonAsync<ProblemDto>())!.Code);
    }
}
```

## Related

[listing lifecycle](../domain/listing-lifecycle.md) · [trust and safety](../domain/trust-and-safety.md) · [data model](../tech/data-model.md)
