using BasaChai.Application.Moderation;

namespace BasaChai.Api.Tests;

public class ModerationTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => f.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact] // MOD-API01: MOD-6
    public async Task MOD_API01_Moderators_cannot_reach_admin_only_endpoints()
    {
        var mod = await f.AsAsync("moderator");
        Assert.Equal(HttpStatusCode.Forbidden, (await mod.GetAsync("/v1/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mod.PostAsJsonAsync("/v1/admin/amenities", new { Key = "x" })).StatusCode);
    }

    [Fact] // MOD-API02: MOD-7
    public async Task MOD_API02_Approve_writes_moderation_action_audit_and_expiry()
    {
        var mod = await f.AsAsync("moderator");
        (await mod.PostAsync("/v1/admin/listings/P1/approve", null)).EnsureSuccessStatusCode();
        await using var db = f.Db();
        var p1 = await db.Listings.SingleAsync(l => l.ShortId == "P1");
        Assert.Equal(ListingStatus.Published, p1.Status);
        Assert.Equal(f.Clock.GetUtcNow().AddDays(30), p1.ExpiresAt);
        Assert.True(await db.ModerationActions.AnyAsync(a => a.ListingId == p1.Id && a.Action == "approve"));
        Assert.True(await db.AuditLog.AnyAsync(a => a.EntityId == p1.Id.ToString() && a.Action == "listing.approve"));
    }

    [Fact] // MOD-API03: concurrent moderators
    public async Task MOD_API03_Second_moderator_gets_already_handled()
    {
        var a = await f.AsAsync("moderator");
        var b = await f.AsAsync("admin");
        (await a.PostAsync("/v1/admin/listings/P2/approve", null)).EnsureSuccessStatusCode();
        var res = await b.PostAsJsonAsync("/v1/admin/listings/P2/reject", new { Reason = "Late reject" });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("already_handled", (await res.ProblemAsync()).Code);
    }

    [Fact] // MOD-1: counts and queue order
    public async Task Queue_is_oldest_first_and_counts_match()
    {
        var mod = await f.AsAsync("moderator");
        var stats = await mod.GetFromJsonAsync<StatsDto>("/v1/admin/stats");
        Assert.Equal((3, 15, 2, 2), (stats!.Pending, stats.Live, stats.OpenReports, stats.Overdue));
        var queue = await mod.GetFromJsonAsync<List<QueueItemDto>>("/v1/admin/listings?status=pending");
        Assert.Equal(["P1", "M4", "P2"], queue!.Select(q => q.ShortId));
        Assert.Equal([true, false, false], queue!.Select(q => q.Overdue));
    }

    [Fact] // MOD-2
    public async Task Reject_without_a_reason_changes_nothing()
    {
        var mod = await f.AsAsync("moderator");
        var res = await mod.PostAsJsonAsync("/v1/admin/listings/P2/reject", new { Reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        await using var db = f.Db();
        Assert.Equal(ListingStatus.Pending, (await db.Listings.SingleAsync(l => l.ShortId == "P2")).Status);
    }

    [Fact] // MOD-4 and the v1.2 rule: hiding closes every open report on the listing
    public async Task Hiding_from_a_report_closes_every_open_report_on_that_listing()
    {
        var anon = await f.AsAsync("anonymous");
        (await anon.PostAsJsonAsync("/v1/listings/Z2/reports", new { Reason = "inaccurate" })).EnsureSuccessStatusCode();
        var mod = await f.AsAsync("moderator");
        var reports = await mod.GetFromJsonAsync<List<ReportDto>>("/v1/admin/reports");
        Assert.Equal(2, reports!.Count(r => r.ListingShortId == "Z2"));
        var r1 = reports!.First(r => r.ListingShortId == "Z2" && r.Reason == "suspicious");
        (await mod.PostAsJsonAsync($"/v1/admin/reports/{r1.Id}/resolve", new { Action = "hide" })).EnsureSuccessStatusCode();
        var open = await mod.GetFromJsonAsync<List<ReportDto>>("/v1/admin/reports");
        Assert.Equal(["I2"], open!.Select(r => r.ListingShortId));
        Assert.DoesNotContain((await anon.GetFromJsonAsync<SearchPage>("/v1/listings?area=bazar"))!.Items, c => c.ShortId == "Z2");
    }

    [Fact] // MOD-5: history shows Live edits with before and after
    public async Task History_shows_live_edits()
    {
        var owner = await f.AsAsync("owner");
        (await owner.PatchAsJsonAsync("/v1/owner/listings/Z1", new { MonthlyRent = 12_500 })).EnsureSuccessStatusCode();
        var mod = await f.AsAsync("moderator");
        var history = await mod.GetFromJsonAsync<List<HistoryItemDto>>("/v1/admin/listings/Z1/history");
        var edit = Assert.Single(history!, h => h.Kind == "edit");
        Assert.Equal(12_000, edit.Before!.Value.GetProperty("monthlyRent").GetInt32());
        Assert.Equal(12_500, edit.After!.Value.GetProperty("monthlyRent").GetInt32());
    }

    [Fact]
    public async Task Request_changes_shows_the_reason_to_the_owner()
    {
        var mod = await f.AsAsync("moderator");
        (await mod.PostAsJsonAsync("/v1/admin/listings/M4/request-changes", new { Reason = "Add a kitchen photo" })).EnsureSuccessStatusCode();
        var owner = await f.AsAsync("owner");
        var m4 = await owner.GetFromJsonAsync<OwnerListingDto>("/v1/owner/listings/M4");
        Assert.Equal(("changes", "Add a kitchen photo"), (m4!.Status, m4.ModeratorReason));
    }

    [Fact]
    public async Task Anonymous_and_members_get_401_and_403()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await (await f.AsAsync("anonymous")).GetAsync("/v1/admin/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await f.AsAsync("tenant")).GetAsync("/v1/admin/stats")).StatusCode);
    }
}
