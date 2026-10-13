namespace BasaChai.Api.Tests;

public class PropertyTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => f.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact] // PROP-API01: PROP-2
    public async Task PROP_API01_Contact_returns_phone_counts_reveal_and_records_event()
    {
        var client = await f.AsAsync("anonymous");
        var res = await client.PostAsync("/v1/listings/D3/contact", null);
        res.EnsureSuccessStatusCode();
        Assert.Equal("+8801900001010", (await res.Content.ReadFromJsonAsync<ContactDto>())!.Phone);
        await using var db = f.Db();
        Assert.Equal(1, await db.ContactEvents.CountAsync(e => e.Listing.ShortId == "D3"));
    }

    [Fact] // PROP-API02: PROP-9, SAFE-5
    public async Task PROP_API02_Detail_never_contains_phone_or_address()
    {
        var client = await f.AsAsync("anonymous");
        var json = await client.GetStringAsync("/v1/listings/D3");
        Assert.DoesNotContain("01900001010", json);
        Assert.DoesNotContain("fullAddress", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] // PROP-1 at the API level
    public async Task Detail_carries_the_cost_breakdown()
    {
        var client = await f.AsAsync("anonymous");
        var d3 = await client.GetFromJsonAsync<ListingDetailDto>("/v1/listings/D3");
        Assert.True(d3!.Available);
        Assert.Equal((10_500, 1_200, 12_780), (d3.Card!.Rent, d3.Card.ServiceCharge, d3.Card.MonthlyTotal));
        Assert.Equal([21_000, 1_000], d3.OneTimeCosts.Select(c => c.Amount!.Value));
        Assert.True(d3.OneTimeCosts[0].IsAdvance);
    }

    [Fact] // PROP-7
    public async Task Listing_past_expiry_is_unavailable_with_no_details()
    {
        var client = await f.AsAsync("anonymous");
        var m2 = await client.GetFromJsonAsync<ListingDetailDto>("/v1/listings/M2");
        Assert.False(m2!.Available);
        Assert.Equal("expired", m2.Status);
        Assert.Null(m2.Card);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/v1/listings/NOPE99")).StatusCode);
    }

    [Fact]
    public async Task Owner_can_preview_a_listing_that_is_not_live()
    {
        var owner = await f.AsAsync("owner");
        var m4 = await owner.GetFromJsonAsync<ListingDetailDto>("/v1/listings/M4");
        Assert.False(m4!.Available);
        Assert.Equal("pending", m4.Status);
        Assert.NotNull(m4.Card);
    }

    [Fact] // PROP-6 at the API level
    public async Task Report_without_a_reason_is_refused()
    {
        var client = await f.AsAsync("anonymous");
        var res = await client.PostAsJsonAsync("/v1/listings/B2/reports", new { Details = "No reason" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("validation_failed", (await res.ProblemAsync()).Code);
    }

    [Fact]
    public async Task Reveal_on_a_listing_that_is_not_live_is_not_found()
    {
        var client = await f.AsAsync("anonymous");
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/v1/listings/M5/contact", null)).StatusCode);
    }
}
