namespace BasaChai.Api.Tests;

/// <summary>
/// Home, area and property pages are cached at the edge for 60 seconds; changes a visitor could see purge them at once
/// (content-and-localization spec, SEO). Drafts were never public, so their autosaves purge nothing.
/// </summary>
public class PageCacheTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public async Task InitializeAsync() { await f.ResetAsync(); f.Pages.Reset(); }
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Editing_a_live_listing_purges_its_page()
    {
        var owner = await f.AsAsync("owner");
        (await owner.PatchAsJsonAsync("/v1/owner/listings/Z1", new { Rules = "No smoking" })).EnsureSuccessStatusCode();
        Assert.True(f.Pages.Purged("Z1"));
    }

    [Fact]
    public async Task Approving_a_listing_purges_its_page()
    {
        var mod = await f.AsAsync("moderator");
        (await mod.PostAsync("/v1/admin/listings/P1/approve", null)).EnsureSuccessStatusCode();
        Assert.True(f.Pages.Purged("P1"));
    }

    [Fact]
    public async Task Draft_autosaves_purge_nothing()
    {
        var owner = await f.AsAsync("owner");
        (await owner.PatchAsJsonAsync("/v1/owner/listings/M1", new { Rules = "No smoking" })).EnsureSuccessStatusCode();
        Assert.Empty(f.Pages.Calls);
    }

    [Fact]
    public async Task An_owner_name_change_purges_their_public_listings()
    {
        var owner = await f.AsAsync("owner");
        (await owner.PatchAsJsonAsync("/v1/me", new { Name = "Renamed Owner", Locale = "bn" })).EnsureSuccessStatusCode();
        Assert.True(f.Pages.Purged("Z1"));
        Assert.DoesNotContain(f.Pages.Calls, c => c.Listings.Contains("M1"));   // their draft has no public page
    }

    [Fact]
    public async Task Changing_the_home_cost_example_purges_everything()
    {
        var admin = await f.AsAsync("admin");
        (await admin.PutAsJsonAsync("/v1/admin/settings/home-cost-example", new { ShortId = "A1" })).EnsureSuccessStatusCode();   // the pilot data uses D3
        Assert.Contains(f.Pages.Calls, c => c.Everything);
    }

    [Fact]
    public async Task Deleting_an_account_purges_its_listings_after_the_commit()
    {
        var owner = await f.AsAsync("owner");
        (await owner.DeleteAsync("/v1/me")).EnsureSuccessStatusCode();
        // The deletion runs in a transaction: one purge, sent after the commit, covering the archived listings.
        var call = Assert.Single(f.Pages.Calls);
        Assert.Contains("Z1", call.Listings);
    }

    [Fact]
    public async Task Hiding_and_unhiding_a_listing_purge_its_page()
    {
        var admin = await f.AsAsync("admin");
        (await admin.PostAsync("/v1/admin/listings/Z1/hide", null)).EnsureSuccessStatusCode();
        Assert.True(f.Pages.Purged("Z1"));
        f.Pages.Reset();
        (await admin.PostAsync("/v1/admin/listings/Z1/unhide", null)).EnsureSuccessStatusCode();
        Assert.True(f.Pages.Purged("Z1"));
    }

    [Fact]
    public async Task Reveals_and_views_do_not_purge()
    {
        var anon = await f.AsAsync("anonymous");
        (await anon.PostAsync("/v1/listings/D3/contact", null)).EnsureSuccessStatusCode();
        await anon.PostAsJsonAsync("/v1/events", new { Name = "listing_viewed", ListingId = "D3", Referrer = "search" });
        Assert.Empty(f.Pages.Calls);
    }
}

/// <summary>Searches shown by the browser are reported through /v1/events, so cached pages still count them.</summary>
public class BrowserSearchEventTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => f.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_reported_search_stores_only_known_filters()
    {
        var anon = await f.AsAsync("anonymous");
        var res = await anon.PostAsJsonAsync("/v1/events", new
        {
            Name = "search_performed", Query = "area=dokkhin&beds=zz&min=5000&phone=01711111111", ResultCount = 3, Source = "home",
        });
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        var e = await f.LastEventAsync("search_performed");
        Assert.Equal(3, e.Props.GetProperty("resultCount").GetInt32());
        Assert.Equal("home", e.Props.GetProperty("source").GetString());
        var filters = e.Props.GetProperty("filters");
        Assert.Equal("dokkhin", filters.GetProperty("area").GetString());
        Assert.Equal(5000, filters.GetProperty("min").GetInt32());
        Assert.False(filters.TryGetProperty("beds", out _));   // invalid value dropped
        Assert.DoesNotContain("01711111111", e.Props.GetRawText());
    }

    [Fact]
    public async Task Unknown_or_incomplete_events_are_refused()
    {
        var anon = await f.AsAsync("anonymous");
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync("/v1/events", new { Name = "search_performed" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync("/v1/events", new { Name = "contact_revealed", ListingId = "D3" })).StatusCode);
    }
}
