using BasaChai.Api.Endpoints;

namespace BasaChai.Api.Tests;

public class AuthTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => f.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact] // AUTH-API01: AUTH-4
    public async Task AUTH_API01_Eleventh_failed_login_within_15_minutes_is_locked_out()
    {
        var client = await f.AsAsync("anonymous");
        var bad = new { Phone = TestUsers.Get("tenant").Phone, Password = "wrong-password" };
        for (var i = 0; i < 10; i++) await client.PostAsJsonAsync("/v1/auth/login", bad);
        var res = await client.PostAsJsonAsync("/v1/auth/login", new { bad.Phone, Password = TestUsers.Password });
        Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);
        f.Clock.Advance(TimeSpan.FromMinutes(16));
        Assert.True((await client.PostAsJsonAsync("/v1/auth/login", new { bad.Phone, Password = TestUsers.Password })).IsSuccessStatusCode);
    }

    [Fact] // AUTH-API02: AUTH-5
    public async Task AUTH_API02_Reset_link_is_single_use_and_audited()
    {
        var admin = await f.AsAsync("admin");
        var tenantId = TestUsers.Get("tenant").Id;
        var link = await (await admin.PostAsync($"/v1/admin/users/{tenantId}/issue-password-reset", null))
            .Content.ReadFromJsonAsync<ResetLinkDto>();
        var anon = await f.AsAsync("anonymous");
        var body = new { link!.Token, NewPassword = "a-new-long-password" };
        Assert.True((await anon.PostAsJsonAsync("/v1/auth/password/reset/confirm", body)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/v1/me")).StatusCode);   // the redeeming device is signed in
        var otherDevice = await f.AsAsync("anonymous");
        Assert.Equal(HttpStatusCode.BadRequest, (await otherDevice.PostAsJsonAsync("/v1/auth/password/reset/confirm", body)).StatusCode);
        await using var db = f.Db();
        Assert.Equal(2, await db.AuditLog.CountAsync(a => a.EntityId == tenantId.ToString() && a.Action.StartsWith("password_reset")));
        var login = await anon.PostAsJsonAsync("/v1/auth/login", new { TestUsers.Get("tenant").Phone, Password = "a-new-long-password" });
        Assert.True(login.IsSuccessStatusCode);
    }

    [Fact] // AUTH-API03: duplicate number
    public async Task AUTH_API03_Registering_an_existing_number_returns_conflict()
    {
        var client = await f.AsAsync("anonymous");
        var res = await client.PostAsJsonAsync("/v1/auth/register",
            new { Name = "Someone", TestUsers.Get("tenant").Phone, Password = "a-long-password" });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("phone_taken", (await res.ProblemAsync()).Code);
    }

    [Fact] // AUTH-1
    public async Task Registration_accepts_Bangla_digits_and_stores_E164()
    {
        var client = await f.AsAsync("anonymous");
        var res = await client.PostAsJsonAsync("/v1/auth/register", new { Name = "Test Person", Phone = "০১৭১২-৩৪৫৬৭৮", Password = "a-long-password" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var me = await client.GetFromJsonAsync<MeDto>("/v1/me");
        Assert.Equal("+8801712345678", me!.Phone);
        Assert.Contains("member", me.Roles);
    }

    [Fact] // AUTH-2 server side, and common passwords
    public async Task Registration_validates_number_name_and_password()
    {
        var client = await f.AsAsync("anonymous");
        var res = await client.PostAsJsonAsync("/v1/auth/register", new { Name = "X", Phone = "12345", Password = "password" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var json = await res.Content.ReadAsStringAsync();
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"phone\"", json);
        Assert.Contains("\"password\"", json);
    }

    [Fact] // AUTH-7 at the API level
    public async Task Staff_without_two_factor_cannot_use_staff_endpoints()
    {
        var fresh = await f.AsAsync("moderator_new");
        var res = await fresh.GetAsync("/v1/admin/stats");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("two_factor_required", (await res.ProblemAsync()).Code);
    }

    [Fact]
    public async Task State_changing_requests_need_the_antiforgery_header_when_signed_in()
    {
        var tenant = await f.AsAsync("tenant");
        tenant.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        var res = await tenant.PutAsync("/v1/me/saved/A1", null);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("csrf_failed", (await res.ProblemAsync()).Code);
    }

    [Fact]
    public async Task Changing_the_password_ends_other_sessions()
    {
        var first = await f.AsAsync("tenant");
        var second = await f.AsAsync("tenant");
        (await first.PostAsJsonAsync("/v1/auth/password/change", new { CurrentPassword = TestUsers.Password, NewPassword = "another-long-password" }))
            .EnsureSuccessStatusCode();
        // The security stamp is checked once time has passed since the cookie was issued; the fake clock must move.
        f.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Suspended_users_cannot_sign_in()
    {
        var admin = await f.AsAsync("admin");
        (await admin.PostAsync($"/v1/admin/users/{TestUsers.Get("tenant").Id}/suspend", null)).EnsureSuccessStatusCode();
        var anon = await f.AsAsync("anonymous");
        var res = await anon.PostAsJsonAsync("/v1/auth/login", new { TestUsers.Get("tenant").Phone, Password = TestUsers.Password });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
