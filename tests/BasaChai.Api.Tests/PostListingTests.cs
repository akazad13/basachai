namespace BasaChai.Api.Tests;

public class PostListingTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => f.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact] // POST-API01: D8
    public async Task POST_API01_Twenty_first_photo_is_refused()
    {
        var owner = await f.AsAsync("owner");
        var id = await owner.CreateDraftAsync();
        for (var i = 0; i < 20; i++) (await owner.UploadPhotoAsync(id, TestFiles.SmallJpeg)).EnsureSuccessStatusCode();
        var res = await owner.UploadPhotoAsync(id, TestFiles.SmallJpeg);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("photo_limit", (await res.ProblemAsync()).Code);
    }

    [Fact] // POST-API02: POST-8
    public async Task POST_API02_Draft_patch_persists_partial_steps()
    {
        var owner = await f.AsAsync("owner");
        var id = await owner.CreateDraftAsync(title: "Partial draft");
        (await owner.PatchAsJsonAsync($"/v1/owner/listings/{id}", new { MonthlyRent = 7000 })).EnsureSuccessStatusCode();
        var draft = await owner.GetFromJsonAsync<OwnerListingDto>($"/v1/owner/listings/{id}");
        Assert.Equal(("Partial draft", 7000, "draft"), (draft!.Title, draft.MonthlyRent, draft.Status));
    }

    [Fact] // The whole posting path: a member becomes an owner, submits, and the listing waits in the queue.
    public async Task A_member_can_post_and_submit_a_complete_listing()
    {
        var tenant = await f.AsAsync("tenant");
        var id = await tenant.CreateDraftAsync(title: "Bright two bedroom flat by the bazar", photos: 3);
        var res = await tenant.PostAsync($"/v1/owner/listings/{id}/submit", null);
        res.EnsureSuccessStatusCode();
        Assert.Equal("pending", (await res.Content.ReadFromJsonAsync<OwnerListingDto>())!.Status);
        var e = await f.LastEventAsync("listing_submitted");
        Assert.True(e.Props.GetProperty("first").GetBoolean());
    }

    [Fact] // POST-4: required fields are checked on submit
    public async Task Submit_lists_missing_fields()
    {
        var owner = await f.AsAsync("owner");
        var res = await owner.PostAsJsonAsync("/v1/owner/listings", new { Title = "Just a title for now" });
        var id = (await res.Content.ReadFromJsonAsync<OwnerListingDto>())!.Id;
        var submit = await owner.PostAsync($"/v1/owner/listings/{id}/submit", null);
        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);
        var json = await submit.Content.ReadAsStringAsync();
        Assert.Contains("monthlyRent", json);
        Assert.Contains("advance", json);
    }

    [Fact] // D23: a Live listing cannot be emptied of a required field
    public async Task Live_edit_keeps_required_fields()
    {
        var owner = await f.AsAsync("owner");
        var res = await owner.PatchAsJsonAsync("/v1/owner/listings/Z1", new { Title = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Full_address_is_stored_encrypted_and_shown_only_to_the_owner()
    {
        var owner = await f.AsAsync("owner");
        var id = await owner.CreateDraftAsync();
        (await owner.PatchAsJsonAsync($"/v1/owner/listings/{id}", new { FullAddress = "House 9, Road 2, Dhamrai" })).EnsureSuccessStatusCode();
        Assert.Equal("House 9, Road 2, Dhamrai", (await owner.GetFromJsonAsync<OwnerListingDto>($"/v1/owner/listings/{id}"))!.FullAddress);
        await using var db = f.Db();
        var stored = (await db.Listings.SingleAsync(l => l.Id == Guid.Parse(id))).FullAddressEnc!;
        Assert.DoesNotContain("House 9", System.Text.Encoding.UTF8.GetString(stored));
    }

    [Fact]
    public async Task Html_is_stripped_from_text_fields()
    {
        var owner = await f.AsAsync("owner");
        var id = await owner.CreateDraftAsync(title: "<b>Flat</b> with <script>x</script>balcony");
        Assert.Equal("Flat with xbalcony", (await owner.GetFromJsonAsync<OwnerListingDto>($"/v1/owner/listings/{id}"))!.Title);
    }
}
