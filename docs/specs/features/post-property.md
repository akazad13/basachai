# Post a property

Owners create a listing in four steps (details, costs, photos, review) and submit it for moderation. Posting is free.

Version 1.0 · 13 October 2026 · Route `/post`, owners only · Covers FR-09, FR-11, D5, D8

## User stories

- As an owner, I can describe my home, list every cost and add photos, then see exactly what tenants will see before submitting.
- As an owner, I can leave and come back without losing a draft.
- As an owner, I am told clearly what is missing before I can continue.

## Entry

- Header "Post a property" (or "New listing" on the dashboard).
- Signed out: sign-in dialog first ("Sign in to post a property."). After sign-in the user becomes an owner.
- A draft listing is created on the server when step 1 is first saved; the draft id is kept in the URL (`/post?draft={id}`).

## Steps

A stepper shows the four numbered steps, the current one highlighted and completed ones marked.

### 1. Home details

| Field | Control | Required |
| --- | --- | --- |
| Title | Text, placeholder "e.g. 2-bedroom flat in Islampur" | Yes |
| Type | Select: Flat, Semi-pucca house | Yes |
| Area | Select: 5 pilot areas | Yes |
| Bedrooms | Select: Studio, 1 to 5 | Yes |
| Bathrooms | Select: 1 to 4 | Yes |
| Size (sq ft) | Number | No |
| Floor | Number (0 = ground floor) | No |
| Furnishing | Select | Yes (default Unfurnished) |
| Who can rent | Select: Anyone, Family, Bachelor, Students | Yes (default Anyone) |
| Available from | Toggle Now / Date, with a date field | Yes |
| Amenities | Checkboxes from the configured list | No |
| Description | Textarea, placeholder about surroundings and light | No |
| Rules | Text | No |
| Full address | Text, hint "This isn't shown publicly. You share it when a tenant calls." | No |

### 2. Costs

| Field | Rule |
| --- | --- |
| Monthly rent (৳) | Required, greater than 0 |
| Monthly service charge (৳) | Required; hint "Enter 0 if there is none." |
| Advance (৳) | Required unless "Don't state an advance amount" is ticked |
| Other costs | Rows of name, amount (optional, blank = varies), Monthly or One-time; "Add a cost" and remove per row |

### 3. Photos

- Hint: "Add at least 3 photos, up to 20. Location data is removed from every photo."
- "Add photo" opens the file picker (JPEG, PNG, WebP, up to 10 MB). Each upload shows "Uploading..." then the thumbnail.
- Each photo has Move earlier, Remove, Move later. The first is marked "First photo" (the cover).
- Counter "{n} of 20 photos"; below 3, "Add {n} more photo(s) to submit".
- Upload errors: wrong type "Use a JPEG, PNG or WebP photo."; too large "Photos must be 10 MB or smaller."; failed "This photo could not be uploaded. Try again."

### 4. Review and submit

- The listing card exactly as tenants will see it, and the cost breakdown.
- Note: "A moderator usually reviews within one business day. You'll see the result on My listings."
- "Submit for review" (brass button).

## Behaviour

- **Live preview:** on desktop (960 px and up) a sticky panel shows the cost breakdown updating as costs are typed.
- **Validation** runs on "Next"; errors show under each field with `aria-invalid`, and focus moves to the first error. "Back" never validates.
- **Autosave:** each step is saved to the draft on "Next" and when leaving the page.
- **Submit** calls the submit action; guards: required fields complete and at least 3 photos stored.
- **Success screen:** check mark, "Submitted for review", the explanation, and buttons "Go to My listings" and "Post another property".
- **Continuing a draft:** "Continue" on the dashboard reopens the step where the owner left off, with everything entered so far.
- **Editing later:** "Edit" on the dashboard opens the same four steps for that listing. For a Live listing, a "Save changes" button sits beside "Next" on every step; saving keeps it Live (D23), writes the audit log and shows "Changes saved. The listing is still live." Changes needed listings are resubmitted with "Submit for review".

## Data and API

- `POST /v1/owner/listings` creates the draft; `PATCH /v1/owner/listings/{id}` saves steps.
- `POST /v1/owner/listings/{id}/photos` (multipart) uploads one photo; `PATCH .../photos` reorders; `DELETE .../photos/{photoId}` removes.
- `POST /v1/owner/listings/{id}/submit` submits.

## Analytics

`listing_submitted` with `first: true` on first submit and `false` on resubmit.

## Acceptance criteria

- **POST-1** Given step 1 with no title and no area, when I press Next, then both errors show, focus moves to Title, and I stay on step 1.
- **POST-2** Given rent 9,000, service 500 and a monthly gas bill of 1,080, then the preview total shows ৳10,580.
- **POST-3** Given "Don't state an advance amount" is ticked, then the advance field is disabled and the preview shows "Not specified".
- **POST-4** Given 2 photos, then the counter says "Add 1 more photo(s) to submit" and I cannot submit.
- **POST-5** Given I upload a 12 MB photo, then it is refused with the size message.
- **POST-6** Given I move the third photo earlier twice, then it becomes the cover on the review card.
- **POST-7** Given a complete listing, when I submit, then its status is In review and it appears in the moderation queue.
- **POST-8** Given I close the browser on step 2, when I return via the dashboard, then my step 1 and 2 data is still there.
- **POST-9** Given I edit the rent of a Live listing, when I save, then it stays Live and the audit log has the before and after rent.

## Out of scope

Agent posting, bulk upload, video, floor plans, price suggestions.

## Test cases

Dataset `pilot`; signed in as `owner`. Photo fixtures are in `web/e2e/files/` (`room-1.jpg` to `room-4.jpg`, each under 1 MB, one with GPS EXIF).

### E2E test cases

| ID | Covers | Preconditions | Steps | Expected |
| --- | --- | --- | --- | --- |
| POST-TC01 | POST-1 | Signed in as `owner` | 1. Open `/{lang}/post`<br>2. Press "Next" with nothing filled | Title and Area errors show; focus on Title; stepper still on step 1 |
| POST-TC02 | POST-2, POST-3 | Step 1 complete | 1. Enter rent 9000, service 500<br>2. Add a monthly cost "Gas bill" 1080<br>3. Tick "Don't state an advance amount" | After step 2 the preview total is ৳10,580; after step 3 the advance field is disabled and the preview shows "Not specified" |
| POST-TC03 | POST-4, POST-5 | Steps 1 and 2 complete | 1. Upload `room-1.jpg` and `room-2.jpg`<br>2. Try a generated 12 MB JPEG | Counter "2 of 20 photos" and "Add 1 more photo(s) to submit"; Next is blocked; the 12 MB file shows the size error and is not added |
| POST-TC04 | POST-6 | 3 photos uploaded | 1. Press "Move earlier" on photo 3 twice<br>2. Go to step 4 | Photo 3 is marked "First photo" and is the cover on the review card |
| POST-TC05 | POST-7 | All steps complete | 1. Press "Submit for review"<br>2. Open `/{lang}/dashboard` | Success screen "Submitted for review"; dashboard shows the new listing with "In review" |
| POST-TC06 | POST-8 | Signed in as `owner` | 1. Complete step 1, press Next<br>2. Enter rent 7000, press Next<br>3. Open `/{lang}/dashboard`, press "Continue" on the draft<br>4. Press "Back" | Step 3: the Costs step reopens with rent 7000. Step 4: step 1 shows the title still filled |
| POST-TC07 | POST-9 | Seed `pilot` | 1. Open Edit on Z1 from the dashboard<br>2. Change rent to 12500 and save | Z1 stays Live; its audit history shows rent 12,000 to 12,500 |

### Automation (E2E)

```ts
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
```

Helpers in `web/e2e/helpers.ts`: `fillStep1(page, t, overrides?)` fills every required step-1 field and presses Next; `fillStep2(page, t)` enters rent 8000, service 0, advance 16000 and presses Next; `publicIdOf(src)` extracts the Cloudinary public ID from a delivery URL. Uploads in E2E go to the test Cloudinary folder (`basachai/test/...`).

### API test cases

| ID | Covers | Case | Expected |
| --- | --- | --- | --- |
| POST-API01 | D8 | Upload a 21st photo to a draft | 409 `photo_limit` |
| POST-API02 | POST-8 | PATCH a draft with only the rent | Draft keeps its title and the new rent; status `draft` |

### Automation (API)

```csharp
// tests/BasaChai.Api.Tests/PostListingTests.cs
public class PostListingTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => f.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact] // POST-API01: D8
    public async Task Twenty_first_photo_is_refused()
    {
        var owner = await f.AsAsync("owner");
        var id = await owner.CreateDraftAsync();                 // helper: POST /v1/owner/listings with valid fields
        for (var i = 0; i < 20; i++) (await owner.UploadPhotoAsync(id, TestFiles.SmallJpeg)).EnsureSuccessStatusCode();
        var res = await owner.UploadPhotoAsync(id, TestFiles.SmallJpeg);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("photo_limit", (await res.Content.ReadFromJsonAsync<ProblemDto>())!.Code);
    }

    [Fact] // POST-API02: POST-8
    public async Task Draft_patch_persists_partial_steps()
    {
        var owner = await f.AsAsync("owner");
        var id = await owner.CreateDraftAsync(title: "Partial draft");
        (await owner.PatchAsJsonAsync($"/v1/owner/listings/{id}", new { MonthlyRent = 7000 })).EnsureSuccessStatusCode();
        var draft = await owner.GetFromJsonAsync<OwnerListingDto>($"/v1/owner/listings/{id}");
        Assert.Equal(("Partial draft", 7000, "draft"), (draft!.Title, draft.MonthlyRent, draft.Status));
    }
}
```

## Related

[listing and pricing](../domain/listing-and-pricing.md) · [listing lifecycle](../domain/listing-lifecycle.md) · [photos](../tech/photos.md) · [owner dashboard](owner-dashboard.md)
