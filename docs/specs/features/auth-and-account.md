# Sign-in, registration and account

Users sign in with a Bangladeshi mobile number and a password. Nothing is ever sent to the phone: numbers are not verified (D9), and forgotten passwords are reset by an admin (D10).

Version 1.0 · 13 October 2026 · Sign-in dialog, `/account` · Covers FR-07

## User stories

- As a tenant, I can create an account with my mobile number and a password to save homes.
- As an owner, I can sign in before posting a property.
- As a user, I can change my name, language and password, sign out, and delete my account.
- As a user who forgot my password, I can get a reset link from support.

## Sign-in dialog

Opened from: Save (signed out), Post a property (signed out), or the header "Sign in".

| Element | Rule |
| --- | --- |
| Title | "Sign in" |
| Reason line | "Sign in to save homes." or "Sign in to post a property." depending on the trigger |
| Mobile number | `type="tel"`, accepts `01XXXXXXXXX`, `8801XXXXXXXXX`, `+8801XXXXXXXXX`, spaces and dashes, Bangla or Latin digits |
| Password | At least 8 characters |
| Note | "We don't send codes to your phone. If you forget your password, contact support." |
| Actions | Cancel, Sign in; a link "Create an account" switches to registration |

### Registration

Same dialog with a Name field added. On success the user is signed in.

| Field | Rule |
| --- | --- |
| Name | 2 to 60 characters |
| Mobile number | Must match `^(?:\+?880\|0)1[3-9]\d{8}$` after normalisation; must be unique |
| Password | At least 8 characters; rejected if on the common-password list |

### After sign-in

- From Save: the listing is saved and "Added to saved homes" shows.
- From Post a property: the user becomes an owner and goes to `/post`.
- Otherwise: stays on the current page.

## Account page (`/account`)

| Section | Content |
| --- | --- |
| Profile | Name, mobile number (read-only), language (Bangla or English) |
| Password | Current password, new password |
| Sign out | Ends this session |
| Delete account | Confirms, then deletes the account; the user's live listings are archived and personal data removed |

## Password reset (admin-assisted)

1. The user contacts support (Contact/Support page lists the channel).
2. Support confirms identity (working default Q7: name, listings and number on a call).
3. An admin presses "Issue password reset" on the user in the admin area.
4. The system creates a single-use token valid 24 hours and shows the link to the admin to pass on.
5. The user opens the link, sets a new password, and every other session ends.
6. Issuing and redeeming are both written to the audit log.

## Sessions and security

- ASP.NET Core Identity cookie on `.{domain}`: HttpOnly, Secure, SameSite=Lax, sliding 30 days.
- Login lockout: 15 minutes after 10 failed attempts. Registration: at most 5 accounts per IP per day.
- Changing the password ends other sessions.
- Moderators and admins must set up two-factor sign-in with an authenticator app before using staff pages.

## Errors

| Case | Message |
| --- | --- |
| Invalid number | "Enter a valid Bangladeshi mobile number, like 01712345678" |
| Short password | "Password must be at least 8 characters" |
| Wrong number or password | "That number and password don't match. Try again or contact support." |
| Number already registered | "This number already has an account. Sign in instead." |
| Locked | "Too many attempts. Try again in 15 minutes." |

## Acceptance criteria

- **AUTH-1** Given I enter "০১৭১২৩৪৫৬৭৮" and a valid password, when I register, then the account is created with phone `+8801712345678`.
- **AUTH-2** Given an invalid number, then the number error shows inline and the form is not sent.
- **AUTH-3** Given I press Save while signed out, when I sign in, then the home is saved without pressing Save again.
- **AUTH-4** Given 10 failed logins, then the 11th attempt within 15 minutes is refused with the lockout message.
- **AUTH-5** Given an admin issues a reset link, when it is used once, then a second use is refused and both events are in the audit log.
- **AUTH-6** Given any sign-up or sign-in flow, then no SMS, email or code is sent.
- **AUTH-7** Given a moderator without two-factor set up, when they open `/admin`, then they are sent to two-factor setup first.

## Out of scope

Phone verification (Phase 2), social login, email addresses, self-service reset.

## Test cases

Dataset `pilot`. The seed also contains `moderator_new`, a moderator without two-factor sign-in, for AUTH-TC04.

### E2E test cases

| ID | Covers | Preconditions | Steps | Expected |
| --- | --- | --- | --- | --- |
| AUTH-TC01 | AUTH-1 | Seed `pilot`; signed out | 1. Press "Sign in", then "Create an account"<br>2. Enter name, number `০১৭১২৩৪৫৬৭৮`, a valid password<br>3. Submit | Signed in; header shows "Saved"; `GET /v1/me` returns phone `+8801712345678` |
| AUTH-TC02 | AUTH-2 | Signed out | 1. Open the sign-in dialog<br>2. Enter number `12345` and a password<br>3. Press "Sign in" | Number error shows under the field with `aria-invalid="true"`; no request is sent to `/v1/auth/login` |
| AUTH-TC03 | AUTH-3 | Seed `pilot`; signed out | 1. Open `/{lang}/listing/A1`<br>2. Press "Save"<br>3. Sign in as `tenant` in the dialog | Dialog says "Sign in to save homes."; after sign-in, the button shows "Saved" and the added-to-saved message shows |
| AUTH-TC04 | AUTH-7 | Seed `pilot`; signed in as `moderator_new` | 1. Open `/{lang}/admin` | Redirected to two-factor setup; the queue is not shown |
| AUTH-TC05 | AUTH-6 | Seed `pilot`; signed out | 1. Record all network requests<br>2. Register a new account | Requests go only to the web and API origins (and Cloudinary images); no SMS or email provider is called; no code entry is ever shown |

### Automation (E2E)

```ts
// web/e2e/auth.spec.ts
import type { Page } from '@playwright/test';
import { test, expect } from './fixtures';
import { signInInDialog } from './helpers';

test.beforeEach(async ({ seed }) => { await seed('pilot'); });

async function openRegister(page: Page, t: (key: string) => string) {
  await page.getByRole('button', { name: t('nav_signin') }).click();
  await page.getByRole('button', { name: t('create_account') }).click();
}

test('AUTH-TC01 register with Bangla digits', async ({ page, lang, t }) => {
  await page.goto(`/${lang}`);
  await openRegister(page, t);
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel(t('name')).fill('Test Person');
  await dialog.getByLabel(t('phone')).fill('০১৭১২৩৪৫৬৭৮');
  await dialog.getByLabel(t('password')).fill(process.env.E2E_PASSWORD!);
  await dialog.getByRole('button', { name: t('create_account') }).click();
  await expect(page.getByRole('link', { name: t('nav_saved') })).toBeVisible();
  const me = await (await page.request.get(`${process.env.E2E_API_URL}/v1/me`)).json();
  expect(me.phone).toBe('+8801712345678');
});

test('AUTH-TC02 invalid number is caught before sending', async ({ page, lang, t }) => {
  let loginCalls = 0;
  page.on('request', r => { if (r.url().includes('/v1/auth/login')) loginCalls++; });
  await page.goto(`/${lang}`);
  await page.getByRole('button', { name: t('nav_signin') }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel(t('phone')).fill('12345');
  await dialog.getByLabel(t('password')).fill('long-enough-pass');
  await dialog.getByRole('button', { name: t('signin_btn') }).click();
  await expect(dialog.getByLabel(t('phone'))).toHaveAttribute('aria-invalid', 'true');
  await expect(dialog.getByText(t('phone_err'))).toBeVisible();
  expect(loginCalls).toBe(0);
});

test('AUTH-TC03 save after sign-in completes the save', async ({ page, lang, t }) => {
  await page.goto(`/${lang}/listing/A1`);
  await page.getByRole('button', { name: t('save') }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText(t('signin_why_save'));
  await signInInDialog(dialog, 'tenant', t);                 // helper fills seeded tenant number and E2E_PASSWORD
  await expect(page.getByRole('button', { name: t('saved') })).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByRole('status')).toHaveText(t('toast_saved'));
});

test('AUTH-TC04 staff without 2FA must set it up', async ({ page, lang, signInAs }) => {
  await signInAs('moderator_new');
  await page.goto(`/${lang}/admin`);
  await expect(page).toHaveURL(new RegExp(`/${lang}/account/two-factor`));
  await expect(page.getByTestId('review-queue')).toHaveCount(0);
});

test('AUTH-TC05 registration sends nothing to the phone', async ({ page, lang, t }) => {
  const hosts = new Set<string>();
  page.on('request', r => hosts.add(new URL(r.url()).host));
  await page.goto(`/${lang}`);
  await openRegister(page, t);
  // ...fill and submit as in AUTH-TC01 with number 01712345679
  const allowed = [new URL(process.env.E2E_WEB_URL!).host, new URL(process.env.E2E_API_URL!).host,
    'res.cloudinary.com', 'images.unsplash.com'];   // pilot dataset photos; fonts are self-hosted
  expect([...hosts].filter(h => !allowed.includes(h))).toEqual([]);
});
```

### API test cases

| ID | Covers | Case | Expected |
| --- | --- | --- | --- |
| AUTH-API01 | AUTH-4 | 10 wrong passwords, then the right one; then 16 minutes later | 429 lockout, then success after the window |
| AUTH-API02 | AUTH-5 | Admin issues a reset link; it is redeemed twice | First redeem succeeds, second returns 400; two audit entries |
| AUTH-API03 | Duplicate number | Register the seeded tenant's number again | 409 `phone_taken` |

### Automation (API)

```csharp
// tests/BasaChai.Api.Tests/AuthTests.cs
public class AuthTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => f.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact] // AUTH-API01: AUTH-4
    public async Task Eleventh_failed_login_within_15_minutes_is_locked_out()
    {
        var client = await f.AsAsync("anonymous");
        var bad = new { Phone = TestUsers.Get("tenant").Phone, Password = "wrong-password" };
        for (var i = 0; i < 10; i++) await client.PostAsJsonAsync("/v1/auth/login", bad);
        var res = await client.PostAsJsonAsync("/v1/auth/login", new { bad.Phone, Password = TestUsers.Password });
        Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);
        f.Clock.Advance(TimeSpan.FromMinutes(16));
        Assert.True((await client.PostAsJsonAsync("/v1/auth/login", new { bad.Phone, Password = TestUsers.Password })).IsSuccessStatusCode);
    }

    [Fact] // AUTH-API02: AUTH-5
    public async Task Reset_link_is_single_use_and_audited()
    {
        var admin = await f.AsAsync("admin");
        var tenantId = TestUsers.Get("tenant").Id;
        var link = await (await admin.PostAsync($"/v1/admin/users/{tenantId}/issue-password-reset", null))
            .Content.ReadFromJsonAsync<ResetLinkDto>();
        var anon = await f.AsAsync("anonymous");
        var body = new { link!.Token, NewPassword = "a-new-long-password" };
        Assert.True((await anon.PostAsJsonAsync("/v1/auth/password/reset/confirm", body)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync("/v1/auth/password/reset/confirm", body)).StatusCode);
        await using var db = f.Db();
        Assert.Equal(2, await db.AuditLog.CountAsync(a => a.EntityId == tenantId.ToString() && a.Action.StartsWith("password_reset")));
    }

    [Fact] // AUTH-API03: duplicate number
    public async Task Registering_an_existing_number_returns_conflict()
    {
        var client = await f.AsAsync("anonymous");
        var res = await client.PostAsJsonAsync("/v1/auth/register",
            new { Name = "Someone", TestUsers.Get("tenant").Phone, Password = "a-long-password" });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("phone_taken", (await res.Content.ReadFromJsonAsync<ProblemDto>())!.Code);
    }
}
```

## Related

[product overview](../product-overview.md#permissions) · [security and operations](../tech/security-and-operations.md) · [API](../tech/api.md#account)
