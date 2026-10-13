# Photos (Cloudinary)

Photos pass through the API to Cloudinary's free plan, which stores a re-encoded original without metadata and serves three fixed sizes from its CDN. The VPS stores no image files and does no image processing (D17).

Version 1.1 · 13 October 2026 · Covers FR-11, D8, D17

## Upload

1. Owner uploads one photo per request: `POST /v1/owner/listings/{id}/photos` (multipart).
2. API checks: signed-in owner of the listing; fewer than 20 photos; size up to 10 MB; file signature is JPEG, PNG or WebP.
3. API streams the file to Cloudinary with the SDK and a **signed upload preset**:
   - `resource_type=image`, allowed formats `jpg`, `png`, `webp`
   - folder `basachai/{env}/listings/{listingId}`
   - **incoming transformation** limiting the original to 1,600 px wide, which re-encodes it and drops metadata, including GPS
4. On success, the API stores `cloudinary_public_id`, `version`, `width`, `height`, `bytes` and the next `sort_order`.
5. Cloudinary rejects anything that is not a valid image; the API returns `photo_type`.

Uploads go through the API (not direct from the browser) so every check is in one place and no unregistered files are left behind.

## Account setup

The API image's `cloudinary-setup` command configures an account from the API's Cloudinary settings, so environments cannot drift: the signed upload preset (formats, 1,600 px incoming limit) and the named transformations below, each marked allowed under strict transformations. It is safe to run again. Turning on **Strict transformations** and two-factor sign-in are console settings with no API, so they stay manual:

```bash
deploy/compose.sh production run --rm --no-deps api cloudinary-setup
```

## Delivery

| Named transformation | Width | Used for |
| --- | --- | --- |
| `card` | 320 px | Cards, thumbnails |
| `detail` | 768 px | Property page on phones, review card |
| `full` | 1,280 px | Property page main photo on desktop |

- Each is `c_limit,w_{width},f_auto,q_auto`: never upscaled, and the browser gets the smallest format it supports.
- **Strict transformations** are on: only these named transformations can be generated, so nobody can burn credits with arbitrary sizes.
- URLs include the version, so a replaced photo never shows stale from a cache.
- Next.js uses a Cloudinary image loader; Next.js image optimisation is off, so Netlify does not bill for resizing.
- Pages show a drawn placeholder underneath while loading or offline.

## Reorder and delete

- `PATCH .../photos` with the new order; the first photo is the cover.
- `DELETE .../photos/{photoId}` removes the row and calls Cloudinary destroy in the same request. If Cloudinary fails, the row is still removed and the public ID is logged at warning level for manual deletion in the Cloudinary console.

## Free-plan budget

Cloudinary meters storage, bandwidth and transformations against a monthly credit allowance (check the current allowance). Rough pilot estimate:

| Use | Assumption | Volume |
| --- | --- | --- |
| Storage | 1,000 listings x 8 photos x 300 KB | about 2.4 GB in total |
| Transformations | 200 new listings a month x 8 photos x 3 sizes, plus format variants | 5,000 to 10,000 a month |
| Bandwidth | 50,000 photo views a month x 80 KB | about 4 GB a month |

The admin dashboard shows Cloudinary usage to admins (`GET /v1/admin/photo-usage`, from Cloudinary's Admin API, fetched when the page opens): credits used this month against the allowance, storage, bandwidth and transformations, with a warning from 80%. If Cloudinary cannot be reached, the panel says usage is not available and the rest of the dashboard works. Trigger to move: usage above 80% of the allowance two months running ([roadmap](../roadmap.md)).

## Security

- Cloudinary API secret in the root-only env file; never in the web app.
- Signed uploads only; no unsigned presets.
- Two-factor sign-in on the Cloudinary console.
- Separate folders per environment.

## Acceptance criteria

- **PHOTO-1** Given a phone photo with GPS in its EXIF, when uploaded, then the stored original and every delivered size have no GPS data.
- **PHOTO-2** Given a request for a size other than `card`, `detail` or `full`, then Cloudinary refuses it.
- **PHOTO-3** Given a 12 MB file, then the API returns 413 `photo_too_large` without calling Cloudinary.
- **PHOTO-4** Given a renamed text file with a `.jpg` extension, then the API returns 415 `photo_type`.
- **PHOTO-5** Given a photo is removed, then it disappears from the listing and Cloudinary destroy is called.

## Test cases

PHOTO-API03 to API05 run in every CI build with the hand-written `FakePhotoStore`. PHOTO-API01 and API02 call the real Cloudinary test folder (`basachai/test/...`) and run nightly and before every deploy (`[Trait("Category", "Cloudinary")]`), using the test account's credentials from CI secrets (`Cloudinary__CloudName`, `Cloudinary__ApiKey`, `Cloudinary__ApiSecret`). Elsewhere `[CloudinaryFact]` skips them and says why. The test account is set up with `cloudinary-setup` and strict transformations on. In CI the job sets `REQUIRE_CLOUDINARY=1`, so missing credentials fail rather than skip. PHOTO-API01 uploads `phone-gps.jpg` (3,000 px with GPS), so the 1,600 px incoming limit really re-encodes it; each delivery must be a readable JPEG, PNG or WebP within its size, because an unreadable body must never count as "no GPS". `ExifReaderTests` run in every build and prove the test photos really carry GPS tags, so a passing PHOTO-API01 means something. A deploy can skip the contract tests (`skip_cloudinary`) only when Cloudinary itself is down.

| ID | Covers | Case | Expected |
| --- | --- | --- | --- |
| PHOTO-API01 | PHOTO-1 | Upload `phone-gps.jpg` (3,000 px, has GPS EXIF) through the API's photo store | The stored original and each named size, fetched from Cloudinary, have no EXIF GPS tags |
| PHOTO-API02 | PHOTO-2 | Request the uploaded photo with `t_card`, then with an ad-hoc `w_500` transformation | `t_card` returns 200; `w_500` is refused (400, 401 or 403) |
| PHOTO-API03 | PHOTO-3 | Upload a 12 MB JPEG | 413 `photo_too_large`; `FakePhotoStore` received no upload |
| PHOTO-API04 | PHOTO-4 | Upload a text file renamed `.jpg` | 415 `photo_type`; no upload |
| PHOTO-API05 | PHOTO-5 | Delete a listing photo | Row removed; `FakePhotoStore.Deleted` contains its public ID |

### Automation

```csharp
// tests/BasaChai.Api.Tests/PhotoTests.cs
public class PhotoTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() { f.Photos.Reset(); return f.ResetAsync(); }
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact] // PHOTO-API03
    public async Task Oversized_photo_is_refused_before_upload()
    {
        var owner = await f.AsAsync("owner");
        var id = await owner.CreateDraftAsync();
        var res = await owner.UploadPhotoAsync(id, new byte[12 * 1024 * 1024], "big.jpg", "image/jpeg");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, res.StatusCode);
        Assert.Empty(f.Photos.Uploaded);
    }

    [Fact] // PHOTO-API04
    public async Task Non_image_with_jpg_name_is_refused()
    {
        var owner = await f.AsAsync("owner");
        var id = await owner.CreateDraftAsync();
        var res = await owner.UploadPhotoAsync(id, "not an image"u8.ToArray(), "fake.jpg", "image/jpeg");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, res.StatusCode);
        Assert.Empty(f.Photos.Uploaded);
    }

    [Fact] // PHOTO-API05
    public async Task Deleting_a_photo_destroys_it_in_the_store()
    {
        var owner = await f.AsAsync("owner");
        var id = await owner.CreateDraftAsync(photos: 3);
        var photo = (await owner.GetFromJsonAsync<OwnerListingDto>($"/v1/owner/listings/{id}"))!.Photos[0];
        (await owner.DeleteAsync($"/v1/owner/listings/{id}/photos/{photo.Id}")).EnsureSuccessStatusCode();
        Assert.Contains(photo.PublicId, f.Photos.Deleted);
    }
}

// tests/BasaChai.Api.Tests/CloudinaryContractTests.cs
[Trait("Category", "Cloudinary")]
public class CloudinaryContractTests(CloudinaryFixture c) : IClassFixture<CloudinaryFixture>
{
    [CloudinaryFact] // PHOTO-API01: PHOTO-1
    public async Task PHOTO_API01_Stored_and_delivered_images_have_no_gps()
    {
        // A phone-sized photo with GPS, so the 1,600 px incoming limit really re-encodes it.
        var uploaded = await c.UploadAsync("phone-gps.jpg");
        Assert.Equal(1600, uploaded.Width);

        (string Url, int MaxWidth)[] deliveries =
            [(c.OriginalUrl(uploaded), 1600), (c.Url(uploaded, "card"), 320), (c.Url(uploaded, "detail"), 768), (c.Url(uploaded, "full"), 1280)];
        foreach (var (url, maxWidth) in deliveries)
        {
            using var res = await c.Http.GetAsync(url);
            Assert.True(res.StatusCode == HttpStatusCode.OK, $"{url}: {(int)res.StatusCode}");
            Assert.Matches("^image/(jpeg|png|webp)$", res.Content.Headers.ContentType?.MediaType ?? "");
            var image = ExifReader.Read(await res.Content.ReadAsByteArrayAsync());   // throws if it is not a readable image
            Assert.False(image.HasGps, url);
            Assert.InRange(image.Width, 1, maxWidth);
        }
    }

    [CloudinaryFact] // PHOTO-API02: PHOTO-2
    public async Task PHOTO_API02_Only_named_transformations_are_served()
    {
        var uploaded = await c.UploadAsync("room-1.jpg");
        using var named = await c.Http.GetAsync(c.Url(uploaded, "card"));
        Assert.Equal(HttpStatusCode.OK, named.StatusCode);
        // Strict transformations refuse the ad-hoc size itself (not a missing photo, a redirect or an outage).
        using var adHoc = await c.Http.GetAsync(c.AdHocUrl(uploaded, "w_500"));
        Assert.Contains(adHoc.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
    }
}
```

`FakePhotoStore` implements `IPhotoStore` by recording calls in `Uploaded` and `Deleted` and returning predictable public IDs; its `Usage` and `UsageFails` drive the usage panel tests. `CloudinaryFixture` uploads into `basachai/test/listings/contract` and destroys what it uploaded afterwards. `ExifReader.HasGps` uses MetadataExtractor.

## Related

[post a property](../features/post-property.md) · [architecture](architecture.md) · [API](api.md#owner)
