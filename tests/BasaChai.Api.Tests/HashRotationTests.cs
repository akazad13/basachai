using BasaChai.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BasaChai.Api.Tests;

/// <summary>
/// IPs, devices and sessions are stored hashed with a salt that rotates every calendar month (security spec, Privacy).
/// Rate limits use stable keys, so the rotation never resets them. Each test starts at a month boundary it computes from
/// the shared fake clock (which only moves forward), so the order the tests run in does not matter.
/// </summary>
public class HashRotationTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => f.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private string Period(int monthsFromNow = 0) => HashSalts.PeriodOf(f.Clock.GetUtcNow().AddMonths(monthsFromNow));

    /// <summary>Moves the clock to the 2nd of next month, 10:00 UTC.</summary>
    private void NextMonth()
    {
        var now = f.Clock.GetUtcNow();
        var target = new DateTimeOffset(now.Year, now.Month, 2, 10, 0, 0, TimeSpan.Zero).AddMonths(1);
        f.Clock.Advance(target - now);
    }

    private async Task<string> ReportAndReadHashAsync(HttpClient client, string shortId)
    {
        (await client.PostAsJsonAsync($"/v1/listings/{shortId}/reports", new { Reason = "inaccurate" })).EnsureSuccessStatusCode();
        await using var db = f.Db();
        return await db.Reports.Where(r => r.Listing.ShortId == shortId).OrderByDescending(r => r.CreatedAt).Select(r => r.ReporterIpHash).FirstAsync();
    }

    private async Task<string[]> SaltPeriodsAsync()
    {
        await using var db = f.Db();
        return await db.HashSalts.Select(s => s.Period).OrderBy(p => p).ToArrayAsync();
    }

    [Fact]
    public async Task Stored_hashes_match_within_a_month_and_change_with_the_next_one()
    {
        var anon = await f.AsAsync("anonymous");
        var first = await ReportAndReadHashAsync(anon, "B2");
        Assert.Equal(first, await ReportAndReadHashAsync(anon, "B3"));

        var before = Period();
        NextMonth();
        Assert.NotEqual(first, await ReportAndReadHashAsync(anon, "D1"));
        Assert.Equal([before, Period()], await SaltPeriodsAsync());
    }

    [Fact]
    public async Task Device_and_session_hashes_rotate_but_rate_limit_keys_do_not()
    {
        var hasher = f.Services.GetRequiredService<IHasher>();
        var salts = f.Services.GetRequiredService<HashSalts>();
        await salts.EnsureAsync();
        string device = hasher.Stored("device:abc"), session = hasher.Stored("session:abc"), key = hasher.Hash("device:abc");
        NextMonth();
        await salts.EnsureAsync();
        Assert.NotEqual(device, hasher.Stored("device:abc"));
        Assert.NotEqual(session, hasher.Stored("session:abc"));
        Assert.Equal(key, hasher.Hash("device:abc"));
    }

    [Fact]
    public async Task A_restart_within_the_month_keeps_the_same_hashes()
    {
        var anon = await f.AsAsync("anonymous");
        var before = await ReportAndReadHashAsync(anon, "B2");
        f.Services.GetRequiredService<HashSalts>().Forget();   // as after a restart: the salt is read back from the database
        Assert.Equal(before, await ReportAndReadHashAsync(anon, "B3"));
    }

    [Fact]
    public async Task Parallel_first_requests_of_a_month_agree_on_one_salt()
    {
        NextMonth();
        f.Services.GetRequiredService<HashSalts>().Forget();
        var anon = f.CreateClient();
        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => anon.GetAsync("/v1/locations")));
        Assert.Contains(Period(), await SaltPeriodsAsync());
        await using var db = f.Db();
        Assert.Equal(1, await db.HashSalts.CountAsync(s => s.Period == Period()));
    }

    [Fact]
    public async Task Salts_older_than_last_month_are_deleted()
    {
        var anon = await f.AsAsync("anonymous");
        await ReportAndReadHashAsync(anon, "B2");
        NextMonth();
        await anon.GetAsync("/v1/locations");
        NextMonth();
        await anon.GetAsync("/v1/locations");
        Assert.Equal([Period(-1), Period()], await SaltPeriodsAsync());
    }
}

/// <summary>The reveal limit holds across the month boundary, when the stored-hash salt rotates.</summary>
public class RotationRateLimitTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task The_reveal_limit_does_not_reset_when_the_month_changes()
    {
        // 23:30 on the last day of the month, with fresh seed data at that time.
        var now = f.Clock.GetUtcNow();
        var lastDay = new DateTimeOffset(now.Year, now.Month, 1, 23, 30, 0, TimeSpan.Zero).AddMonths(1).AddDays(-1);
        f.Clock.Advance(lastDay - now);
        await f.ResetAsync();

        var anon = await f.AsAsync("anonymous");
        string[] ids = ["A1", "A2", "A3", "B1", "B2", "B3", "D1", "D2", "D3", "Z2"];
        foreach (var id in ids) Assert.Equal(HttpStatusCode.OK, (await anon.PostAsync($"/v1/listings/{id}/contact", null)).StatusCode);

        f.Clock.Advance(TimeSpan.FromMinutes(40));   // 00:10 on the 1st: a new salt, but within the same hour
        Assert.Equal(HttpStatusCode.TooManyRequests, (await anon.PostAsync("/v1/listings/Z3/contact", null)).StatusCode);

        // The salt really rotated in between.
        await using var db = f.Db();
        Assert.Equal(2, await db.HashSalts.CountAsync());
    }
}
