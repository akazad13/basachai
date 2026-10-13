using System.Net.Http.Headers;
using BasaChai.Api.Endpoints;
using BasaChai.Application.Moderation;

namespace BasaChai.Api.Tests;

/// <summary>Regression tests for the issues found in the first backend review.</summary>
public class ReviewRegressionTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => f.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Deleting_an_account_archives_everything_frees_the_number_and_removes_personal_data()
    {
        var owner = await f.AsAsync("owner");
        var res = await owner.DeleteAsync("/v1/me");
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        await using var db = f.Db();
        var id = TestUsers.Get("owner").Id;
        var listings = await db.Listings.Where(l => l.OwnerId == id).ToListAsync();
        Assert.All(listings, l => Assert.Equal(ListingStatus.Archived, l.Status));   // M4 was in review
        Assert.All(listings, l => Assert.Null(l.FullAddressEnc));
        var user = await db.Users.SingleAsync(u => u.Id == id);
        Assert.Equal("Deleted user", user.DisplayName);
        Assert.Null(user.PasswordHash);
        var anon = await f.AsAsync("anonymous");
        var again = await anon.PostAsJsonAsync("/v1/auth/register", new { Name = "New person", TestUsers.Get("owner").Phone, Password = "a-long-password" });
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task Parallel_reveals_and_views_on_one_listing_all_succeed_and_all_count()
    {
        var clients = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => f.AsAsync("anonymous")));
        var reveals = await Task.WhenAll(clients.Select(c => c.PostAsync("/v1/listings/D3/contact", null)));
        Assert.All(reveals, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var views = await Task.WhenAll(clients.Select(c => c.PostAsJsonAsync("/v1/events", new { Name = "listing_viewed", ListingId = "D3" })));
        Assert.All(views, r => Assert.Equal(HttpStatusCode.Accepted, r.StatusCode));
        await using var db = f.Db();
        var d3 = await db.Listings.SingleAsync(l => l.ShortId == "D3");
        var seeded = TestData.Listings.First(l => l.GetProperty("id").GetString() == "D3");
        Assert.Equal(seeded.GetProperty("reveals").GetInt32() + 8, d3.RevealsCount);
        Assert.Equal(seeded.GetProperty("views").GetInt32() + 8, d3.ViewsCount);
    }

    [Fact]
    public async Task Counters_do_not_make_an_owner_edit_stale()
    {
        var owner = await f.AsAsync("owner");
        var z1 = await owner.GetFromJsonAsync<OwnerListingDto>("/v1/owner/listings/Z1");
        var anon = await f.AsAsync("anonymous");
        (await anon.PostAsync("/v1/listings/Z1/contact", null)).EnsureSuccessStatusCode();
        var patch = new HttpRequestMessage(HttpMethod.Patch, "/v1/owner/listings/Z1") { Content = JsonContent.Create(new { MonthlyRent = 12_300 }) };
        patch.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{z1!.Version}\""));
        Assert.Equal(HttpStatusCode.OK, (await owner.SendAsync(patch)).StatusCode);
    }

    [Fact]
    public async Task A_stale_edit_is_refused_with_stale_listing()
    {
        var owner = await f.AsAsync("owner");
        var z1 = await owner.GetFromJsonAsync<OwnerListingDto>("/v1/owner/listings/Z1");
        (await owner.PatchAsJsonAsync("/v1/owner/listings/Z1", new { MonthlyRent = 12_100 })).EnsureSuccessStatusCode();
        var patch = new HttpRequestMessage(HttpMethod.Patch, "/v1/owner/listings/Z1") { Content = JsonContent.Create(new { MonthlyRent = 12_200 }) };
        patch.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{z1!.Version}\""));
        var res = await owner.SendAsync(patch);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("stale_listing", (await res.ProblemAsync()).Code);
    }

    [Fact]
    public async Task Parallel_dashboard_loads_after_expiry_all_succeed()
    {
        f.Clock.Advance(TimeSpan.FromDays(3));
        var owner = await f.AsAsync("owner");
        var loads = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => owner.GetAsync("/v1/owner/listings")));
        Assert.All(loads, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        await using var db = f.Db();
        Assert.Equal(1, await db.AvailabilityHistory.CountAsync(h => h.Listing.ShortId == "Z1" && h.ToStatus == "expired"));
    }

    [Fact]
    public async Task Dashboard_rows_follow_the_spec_order()
    {
        var owner = await f.AsAsync("owner");
        var rows = await owner.GetFromJsonAsync<List<OwnerListingDto>>("/v1/owner/listings");
        Assert.Equal(["changes", "draft", "expired", "pending", "published", "rented"], rows!.Select(r => r.Status));
    }

    [Fact]
    public async Task Parallel_wrong_passwords_cannot_exceed_the_lockout()
    {
        var clients = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => f.AsAsync("anonymous")));
        var bad = new { TestUsers.Get("tenant").Phone, Password = "wrong-password" };
        var codes = (await Task.WhenAll(clients.Select(c => c.PostAsJsonAsync("/v1/auth/login", bad)))).Select(r => r.StatusCode).ToList();
        Assert.Equal(10, codes.Count(c => c == HttpStatusCode.Unauthorized));
        Assert.Equal(10, codes.Count(c => c == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task Successful_logins_do_not_count_toward_the_lockout()
    {
        var anon = await f.AsAsync("anonymous");
        for (var i = 0; i < 12; i++)
            Assert.True((await anon.PostAsJsonAsync("/v1/auth/login", new { TestUsers.Get("tenant").Phone, Password = TestUsers.Password })).IsSuccessStatusCode);
    }

    [Fact]
    public async Task History_lists_a_moderator_action_once_and_not_as_an_edit()
    {
        var mod = await f.AsAsync("moderator");
        (await mod.PostAsync("/v1/admin/listings/P1/approve", null)).EnsureSuccessStatusCode();
        var history = await mod.GetFromJsonAsync<List<HistoryItemDto>>("/v1/admin/listings/P1/history");
        Assert.DoesNotContain(history!, h => h.Kind == "edit");
        Assert.Single(history!, h => h.Kind == "moderation");
    }

    [Fact]
    public async Task Cacheable_public_reads_never_set_cookies()
    {
        var anon = await f.AsAsync("anonymous");
        foreach (var path in new[] { "/v1/listings?area=dokkhin", "/v1/listings/D3", "/v1/home", "/v1/locations" })
        {
            var res = await anon.GetAsync(path);
            Assert.False(res.Headers.Contains("Set-Cookie"), path);
        }
        Assert.True((await anon.PostAsync("/v1/listings/D3/contact", null)).Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData("/v1/listings?cursor=eyJTIjoiY29uZmlybWVkIiwiViI6LTUsIksiOiJBMSJ9", HttpStatusCode.OK)]   // negative ticks: ignored
    [InlineData("/v1/admin/listings?status=foo", HttpStatusCode.BadRequest)]
    [InlineData("/v1/admin/audit?from=2026-10-01T00:00:00%2B06:00", HttpStatusCode.Forbidden)]   // moderator: admin only, but no 500
    public async Task Bad_input_never_returns_500(string path, HttpStatusCode expected)
    {
        var mod = await f.AsAsync("moderator");
        Assert.Equal(expected, (await mod.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Audit_filter_accepts_non_utc_offsets()
    {
        var admin = await f.AsAsync("admin");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/v1/admin/audit?from=2026-10-01T00:00:00%2B06:00")).StatusCode);
    }

    [Fact]
    public async Task Restricted_owners_can_take_listings_down_but_not_change_or_renew_them()
    {
        var admin = await f.AsAsync("admin");
        (await admin.PostAsync($"/v1/admin/users/{TestUsers.Get("owner").Id}/restrict", null)).EnsureSuccessStatusCode();
        var owner = await f.AsAsync("owner");
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PatchAsJsonAsync("/v1/owner/listings/Z1", new { MonthlyRent = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsync("/v1/owner/listings/Z1/reconfirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync("/v1/owner/listings/Z1/pause", null)).StatusCode);
    }

    [Fact]
    public async Task Listings_that_never_went_public_do_not_exist_for_visitors()
    {
        var anon = await f.AsAsync("anonymous");
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/v1/listings/P1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.PostAsJsonAsync("/v1/listings/M1/reports", new { Reason = "inaccurate" })).StatusCode);
        var tenant = await f.AsAsync("tenant");
        Assert.Equal(HttpStatusCode.NotFound, (await tenant.PutAsync("/v1/me/saved/P1", null)).StatusCode);
    }

    [Fact]
    public async Task Two_factor_key_is_shown_only_while_enrolling_and_enrolling_needs_the_password()
    {
        var mod = await f.AsAsync("moderator");
        var enrolled = await mod.GetFromJsonAsync<System.Text.Json.JsonElement>("/v1/me/two-factor");
        Assert.Equal(System.Text.Json.JsonValueKind.Null, enrolled.GetProperty("sharedKey").ValueKind);
        var fresh = await f.AsAsync("moderator_new");
        var res = await fresh.PostAsJsonAsync("/v1/me/two-factor", new { Code = "123456", Password = "not-the-password" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Admins_cannot_suspend_themselves()
    {
        var admin = await f.AsAsync("admin");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/v1/admin/users/{TestUsers.Get("admin").Id}/suspend", null)).StatusCode);
    }

    [Fact]
    public async Task A_suspended_owners_listing_cannot_be_approved()
    {
        var admin = await f.AsAsync("admin");
        (await admin.PostAsync($"/v1/admin/users/{TestUsers.Get("owner").Id}/suspend", null)).EnsureSuccessStatusCode();
        var res = await admin.PostAsync("/v1/admin/listings/M4/approve", null);
        Assert.Equal("owner_unavailable", (await res.ProblemAsync()).Code);
    }
}
