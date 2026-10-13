namespace BasaChai.Api.Tests;

public class SafetyTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => f.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact] // SAFE-API01: SAFE-2
    public async Task SAFE_API01_Reveal_limit_is_ten_per_hour()
    {
        var anon = await f.AsAsync("anonymous");
        string[] ids = ["A1", "A2", "A3", "B1", "B2", "B3", "D1", "D2", "D3", "Z2", "Z3"];
        var codes = new List<HttpStatusCode>();
        foreach (var id in ids) codes.Add((await anon.PostAsync($"/v1/listings/{id}/contact", null)).StatusCode);
        Assert.All(codes.Take(10), c => Assert.Equal(HttpStatusCode.OK, c));
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[10]);
        var limited = await anon.PostAsync("/v1/listings/I1/contact", null);
        var problem = await limited.ProblemAsync();
        Assert.Equal("rate_limited", problem.Code);
        Assert.InRange(problem.RetryAfter!.Value, 1, 3600);
        f.Clock.Advance(TimeSpan.FromMinutes(61));
        Assert.Equal(HttpStatusCode.OK, (await anon.PostAsync("/v1/listings/I1/contact", null)).StatusCode);
    }

    [Fact] // SAFE-API02: SAFE-3
    public async Task SAFE_API02_Anonymous_report_is_stored_without_a_reporter()
    {
        var anon = await f.AsAsync("anonymous");
        var res = await anon.PostAsJsonAsync("/v1/listings/B2/reports", new { Reason = "inaccurate", Details = "Rent differs" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        await using var db = f.Db();
        var report = await db.Reports.OrderByDescending(r => r.CreatedAt).FirstAsync();
        Assert.Null(report.ReporterId);
        Assert.False(string.IsNullOrEmpty(report.ReporterIpHash));
    }

    [Fact] // SAFE-API03: SAFE-4
    public async Task SAFE_API03_Hidden_listing_leaves_search_home_and_saved()
    {
        var tenant = await f.AsAsync("tenant");
        (await tenant.PutAsync("/v1/me/saved/A1", null)).EnsureSuccessStatusCode();
        Assert.Contains((await tenant.GetFromJsonAsync<List<CardDto>>("/v1/me/saved"))!, c => c.ShortId == "A1");
        var mod = await f.AsAsync("moderator");
        (await mod.PostAsync("/v1/admin/listings/A1/hide", null)).EnsureSuccessStatusCode();
        Assert.DoesNotContain((await tenant.GetFromJsonAsync<SearchPage>("/v1/listings?size=24"))!.Items, c => c.ShortId == "A1");
        Assert.DoesNotContain((await tenant.GetFromJsonAsync<SearchPage>("/v1/listings?sort=confirmed&size=4"))!.Items, c => c.ShortId == "A1");
        Assert.DoesNotContain((await tenant.GetFromJsonAsync<List<CardDto>>("/v1/me/saved"))!, c => c.ShortId == "A1");
    }

    [Fact] // SAFE-API04: SAFE-5
    public async Task SAFE_API04_Public_responses_never_leak_phones_or_addresses()
    {
        var anon = await f.AsAsync("anonymous");
        var bodies = new List<string> { await anon.GetStringAsync("/v1/listings?size=24"), await anon.GetStringAsync("/v1/home") };
        foreach (var id in TestData.LiveIds) bodies.Add(await anon.GetStringAsync($"/v1/listings/{id}"));
        Assert.NotEmpty(TestData.FullAddresses);
        foreach (var body in bodies)
        foreach (var secret in TestData.OwnerPhonesAllFormats.Concat(TestData.FullAddresses))
            Assert.DoesNotContain(secret, body);
    }

    [Fact] // SAVE-2 at the API level: saving is idempotent and rented homes drop off
    public async Task Saved_homes_are_idempotent_and_show_only_live_homes()
    {
        var tenant = await f.AsAsync("tenant");
        (await tenant.PutAsync("/v1/me/saved/Z3", null)).EnsureSuccessStatusCode();
        (await tenant.PutAsync("/v1/me/saved/Z3", null)).EnsureSuccessStatusCode();
        Assert.Single((await tenant.GetFromJsonAsync<List<CardDto>>("/v1/me/saved"))!);
        var owner = await f.AsAsync("owner7");
        (await owner.PostAsync("/v1/owner/listings/Z3/mark-rented", null)).EnsureSuccessStatusCode();
        Assert.Empty((await tenant.GetFromJsonAsync<List<CardDto>>("/v1/me/saved"))!);
        (await tenant.DeleteAsync("/v1/me/saved/Z3")).EnsureSuccessStatusCode();
    }
}

public class AnalyticsTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => f.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact] // ANA-API01: ANA-1
    public async Task ANA_API01_Search_records_source_count_and_filters()
    {
        var anon = await f.AsAsync("anonymous");
        anon.DefaultRequestHeaders.Add("X-Search-Source", "home");
        await anon.GetAsync("/v1/listings?area=dokkhin");
        var e = await f.LastEventAsync("search_performed");
        Assert.Equal(3, e.Props.GetProperty("resultCount").GetInt32());
        Assert.Equal("home", e.Props.GetProperty("source").GetString());
        Assert.Equal("dokkhin", e.Props.GetProperty("filters").GetProperty("area").GetString());
    }

    [Fact] // ANA-API02: ANA-2
    public async Task ANA_API02_Reveal_records_signed_in_flag()
    {
        var anon = await f.AsAsync("anonymous");
        await anon.PostAsync("/v1/listings/D3/contact", null);
        var e = await f.LastEventAsync("contact_revealed");
        Assert.Equal("D3", e.Props.GetProperty("listingId").GetString());
        Assert.False(e.Props.GetProperty("signedIn").GetBoolean());
    }

    [Fact] // ANA-API03: ANA-3
    public async Task ANA_API03_Events_never_hold_personal_data()
    {
        var anon = await f.AsAsync("anonymous");
        await anon.GetAsync("/v1/listings?q=gas");
        await anon.PostAsync("/v1/listings/D3/contact", null);
        await anon.PostAsJsonAsync("/v1/listings/B2/reports", new { Reason = "inaccurate", Details = "Secret note 01711111111" });
        var owner = await f.AsAsync("owner");
        var id = await owner.CreateDraftAsync(photos: 3);
        (await owner.PostAsync($"/v1/owner/listings/{id}/submit", null)).EnsureSuccessStatusCode();
        await using var db = f.Db();
        var all = string.Join('\n', (await db.AnalyticsEvents.ToListAsync()).Select(e => e.Props.RootElement.GetRawText()));
        Assert.Contains("listing_submitted", string.Join(',', await db.AnalyticsEvents.Select(e => e.Name).ToListAsync()));
        Assert.DoesNotMatch(@"(\+?880|0)1[3-9]\d{8}", all);
        Assert.DoesNotContain("Secret note", all);
        foreach (var address in TestData.FullAddresses) Assert.DoesNotContain(address, all);
    }

    [Fact]
    public async Task Show_more_pages_and_the_home_widget_are_not_searches()
    {
        var anon = await f.AsAsync("anonymous");
        var first = await anon.GetFromJsonAsync<SearchPage>("/v1/listings?size=2");
        var cursor = first!.NextCursor;
        await anon.GetAsync($"/v1/listings?size=2&cursor={Uri.EscapeDataString(cursor!)}");
        var home = new HttpRequestMessage(HttpMethod.Get, "/v1/listings?size=4");
        home.Headers.Add("X-Search-Source", "none");
        await anon.SendAsync(home);
        await using var db = f.Db();
        Assert.Equal(1, await db.AnalyticsEvents.CountAsync(e => e.Name == "search_performed"));
    }

    [Fact]
    public async Task Views_count_once_per_session_per_hour()
    {
        var anon = await f.AsAsync("anonymous");
        for (var i = 0; i < 3; i++) await anon.PostAsJsonAsync("/v1/events", new { Name = "listing_viewed", ListingId = "D3", Referrer = "search" });
        await using var db = f.Db();
        Assert.Equal(1, await db.AnalyticsEvents.CountAsync(e => e.Name == "listing_viewed"));
    }
}
