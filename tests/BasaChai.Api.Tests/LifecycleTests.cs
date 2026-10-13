namespace BasaChai.Api.Tests;

public class LifecycleTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => f.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact] // LIFE-API01: LIFE-1
    public async Task LIFE_API01_Submit_needs_three_photos()
    {
        var owner = await f.AsAsync("owner");
        var id = await owner.CreateDraftAsync(photos: 2);
        var res = await owner.PostAsync($"/v1/owner/listings/{id}/submit", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("photos_required", (await res.ProblemAsync()).Code);
        Assert.Equal("draft", (await owner.GetFromJsonAsync<OwnerListingDto>($"/v1/owner/listings/{id}"))!.Status);
    }

    [Fact] // LIFE-API02: LIFE-3
    public async Task LIFE_API02_Expiry_applies_at_read_time_and_persists_on_dashboard_visit()
    {
        f.Clock.Advance(TimeSpan.FromDays(3));
        var anon = await f.AsAsync("anonymous");
        var bazar = await anon.GetFromJsonAsync<SearchPage>("/v1/listings?area=bazar");
        Assert.DoesNotContain(bazar!.Items, c => c.ShortId == "Z1");
        var owner = await f.AsAsync("owner");
        var mine = await owner.GetFromJsonAsync<List<OwnerListingDto>>("/v1/owner/listings");
        Assert.Equal("expired", mine!.Single(l => l.ShortId == "Z1").Status);
        await using var db = f.Db();
        Assert.True(await db.AvailabilityHistory.AnyAsync(h => h.Listing.ShortId == "Z1" && h.ToStatus == "expired" && h.ActorType == "system"));
        Assert.Equal(ListingStatus.Expired, (await db.Listings.SingleAsync(l => l.ShortId == "Z1")).Status);
    }

    [Fact] // LIFE-API03: LIFE-6
    public async Task LIFE_API03_Editing_a_live_listing_keeps_it_live_and_audits_the_change()
    {
        var owner = await f.AsAsync("owner");
        (await owner.PatchAsJsonAsync("/v1/owner/listings/Z1", new { MonthlyRent = 12_500 })).EnsureSuccessStatusCode();
        await using var db = f.Db();
        Assert.Equal(ListingStatus.Published, (await db.Listings.SingleAsync(l => l.ShortId == "Z1")).Status);
        var audit = await db.AuditLog.Where(a => a.Action == "listing.edit").OrderByDescending(a => a.CreatedAt).FirstAsync();
        Assert.Equal(12_000, audit.Before!.RootElement.GetProperty("monthlyRent").GetInt32());
        Assert.Equal(12_500, audit.After!.RootElement.GetProperty("monthlyRent").GetInt32());
    }

    [Fact] // LIFE-API04: LIFE-7
    public async Task LIFE_API04_Hide_and_unhide_keep_status_and_expiry()
    {
        var mod = await f.AsAsync("moderator");
        await using var before = f.Db();
        var expires = (await before.Listings.SingleAsync(l => l.ShortId == "D3")).ExpiresAt;
        (await mod.PostAsync("/v1/admin/listings/D3/hide", null)).EnsureSuccessStatusCode();
        var anon = await f.AsAsync("anonymous");
        Assert.DoesNotContain((await anon.GetFromJsonAsync<SearchPage>("/v1/listings?area=dokkhin"))!.Items, c => c.ShortId == "D3");
        (await mod.PostAsync("/v1/admin/listings/D3/unhide", null)).EnsureSuccessStatusCode();
        Assert.Contains((await anon.GetFromJsonAsync<SearchPage>("/v1/listings?area=dokkhin"))!.Items, c => c.ShortId == "D3");
        await using var after = f.Db();
        var d3 = await after.Listings.SingleAsync(l => l.ShortId == "D3");
        Assert.Equal((ListingStatus.Published, expires), (d3.Status, d3.ExpiresAt));
    }

    [Fact] // LIFE-5 through the API
    public async Task Archiving_a_listing_in_review_is_refused()
    {
        var owner = await f.AsAsync("owner");
        var res = await owner.PostAsync("/v1/owner/listings/M4/archive", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("invalid_transition", (await res.ProblemAsync()).Code);
    }

    [Fact]
    public async Task Owners_cannot_touch_other_owners_listings()
    {
        var owner = await f.AsAsync("owner");
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsync("/v1/owner/listings/A1/pause", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync("/v1/owner/listings/A1")).StatusCode);
    }

    [Fact] // DASH-1, DASH-4 at the API level
    public async Task Reconfirm_resets_thirty_days_and_lowers_the_attention_count()
    {
        var owner = await f.AsAsync("owner");
        Assert.Equal(3, (await owner.GetFromJsonAsync<AttentionDto>("/v1/me/attention"))!.Count);
        var z1 = await (await owner.PostAsync("/v1/owner/listings/Z1/reconfirm", null)).Content.ReadFromJsonAsync<OwnerListingDto>();
        Assert.Equal(30, z1!.DaysLeft);
        Assert.Equal(2, (await owner.GetFromJsonAsync<AttentionDto>("/v1/me/attention"))!.Count);
    }

    public sealed record AttentionDto(int Count);
}
