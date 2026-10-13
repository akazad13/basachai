namespace BasaChai.Api.Tests;

public class PhotoTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() { f.Photos.Reset(); return f.ResetAsync(); }
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact] // PHOTO-API03: PHOTO-3
    public async Task PHOTO_API03_Oversized_photo_is_refused_before_upload()
    {
        var owner = await f.AsAsync("owner");
        var id = await owner.CreateDraftAsync();
        var big = new byte[12 * 1024 * 1024];
        TestFiles.SmallJpeg.CopyTo(big, 0);
        var res = await owner.UploadPhotoAsync(id, big, "big.jpg", "image/jpeg");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, res.StatusCode);
        Assert.Equal("photo_too_large", (await res.ProblemAsync()).Code);
        Assert.Empty(f.Photos.Uploaded);
    }

    [Fact] // PHOTO-API04: PHOTO-4
    public async Task PHOTO_API04_Non_image_with_jpg_name_is_refused()
    {
        var owner = await f.AsAsync("owner");
        var id = await owner.CreateDraftAsync();
        var res = await owner.UploadPhotoAsync(id, "not an image"u8.ToArray(), "fake.jpg", "image/jpeg");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, res.StatusCode);
        Assert.Equal("photo_type", (await res.ProblemAsync()).Code);
        Assert.Empty(f.Photos.Uploaded);
    }

    [Fact] // PHOTO-API05: PHOTO-5
    public async Task PHOTO_API05_Deleting_a_photo_destroys_it_in_the_store()
    {
        var owner = await f.AsAsync("owner");
        var id = await owner.CreateDraftAsync(photos: 3);
        var photo = (await owner.GetFromJsonAsync<OwnerListingDto>($"/v1/owner/listings/{id}"))!.Photos[0];
        (await owner.DeleteAsync($"/v1/owner/listings/{id}/photos/{photo.Id}")).EnsureSuccessStatusCode();
        Assert.Contains(photo.PublicId, f.Photos.Deleted);
        Assert.Equal(2, (await owner.GetFromJsonAsync<OwnerListingDto>($"/v1/owner/listings/{id}"))!.Photos.Count);
    }

    [Fact]
    public async Task Reordering_makes_the_chosen_photo_the_cover()
    {
        var owner = await f.AsAsync("owner");
        var id = await owner.CreateDraftAsync(photos: 3);
        var photos = (await owner.GetFromJsonAsync<OwnerListingDto>($"/v1/owner/listings/{id}"))!.Photos;
        var order = new[] { photos[2].Id, photos[0].Id, photos[1].Id };
        (await owner.PatchAsJsonAsync($"/v1/owner/listings/{id}/photos", new { Order = order })).EnsureSuccessStatusCode();
        var after = await owner.GetFromJsonAsync<OwnerListingDto>($"/v1/owner/listings/{id}");
        Assert.Equal(photos[2].PublicId, after!.Cover!.PublicId);
        Assert.Equal(order, after.Photos.Select(p => p.Id));
    }

    [Fact]
    public async Task Uploads_go_to_the_environment_folder()
    {
        var owner = await f.AsAsync("owner");
        var id = await owner.CreateDraftAsync(photos: 1);
        Assert.StartsWith($"basachai/test/listings/{id}/", Assert.Single(f.Photos.Uploaded));
    }

    [Fact]
    public async Task Admins_see_photo_usage_and_the_80_percent_warning()
    {
        f.Photos.Usage = new PhotoUsage("Free", 21.5, 25, 86, 2_400_000_000, 4_000_000_000, 8_000, DateTimeOffset.UtcNow);
        var admin = await f.AsAsync("admin");
        var dto = await admin.GetFromJsonAsync<PhotoUsageDto>("/v1/admin/photo-usage");
        Assert.True(dto!.Available);
        Assert.True(dto.NearLimit);
        Assert.Equal(21.5, dto.Usage!.CreditsUsed);

        var moderator = await f.AsAsync("moderator");
        Assert.Equal(HttpStatusCode.Forbidden, (await moderator.GetAsync("/v1/admin/photo-usage")).StatusCode);
    }

    [Fact]
    public async Task Photo_usage_outage_does_not_break_the_dashboard()
    {
        f.Photos.UsageFails = true;
        var admin = await f.AsAsync("admin");
        var res = await admin.GetAsync("/v1/admin/photo-usage");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.False((await res.Content.ReadFromJsonAsync<PhotoUsageDto>())!.Available);
    }
}
