namespace BasaChai.Api.Tests;

public class SearchTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => f.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact] // SRCH-API01: SRCH-2
    public async Task SRCH_API01_Rent_filter_is_inclusive_and_uses_monthly_rent()
    {
        var client = await f.AsAsync("anonymous");
        var page = await client.GetFromJsonAsync<SearchPage>("/v1/listings?min=8000&max=12000");
        Assert.Equal(7, page!.Count);
        Assert.All(page.Items, c => Assert.InRange(c.Rent, 8_000, 12_000));
        Assert.Contains(page.Items, c => c.ShortId == "Z1" && c.MonthlyTotal == 14_080);
    }

    [Fact] // SRCH-API02: SRCH-6
    public async Task SRCH_API02_Expired_paused_rented_and_hidden_listings_are_excluded()
    {
        var client = await f.AsAsync("anonymous");
        var page = await client.GetFromJsonAsync<SearchPage>("/v1/listings?size=24");
        Assert.Equal(15, page!.Count);
        Assert.DoesNotContain(page.Items, c => c.ShortId is "M2" or "M5" or "P1");
    }

    [Fact] // SRCH-API03: keyset paging is stable
    public async Task SRCH_API03_Paging_returns_every_listing_once_in_order()
    {
        var client = await f.AsAsync("anonymous");
        var first = await client.GetFromJsonAsync<SearchPage>("/v1/listings?size=10");
        var second = await client.GetFromJsonAsync<SearchPage>($"/v1/listings?size=10&cursor={first!.NextCursor}");
        var ids = first.Items.Concat(second!.Items).Select(c => c.ShortId).ToList();
        Assert.Equal(15, ids.Distinct().Count());
        Assert.Null(second.NextCursor);
    }

    [Theory] // SRCH-1, SRCH-3, SRCH-5 and the values the E2E tests rely on
    [InlineData("area=dokkhin", 3)]
    [InlineData("occ=family", 11)]
    [InlineData("area=barat&beds=3", 0)]
    [InlineData("beds=3", 4)]
    [InlineData("area=amin&beds=2", 1)]
    [InlineData("area=amin", 3)]
    [InlineData("area=bazar&beds=2&sort=rent_asc", 1)]
    [InlineData("area=nowhere", 0)]
    [InlineData("beds=abc&sort=bogus&furnish=gold", 15)]
    public async Task Filters_combine_and_ignore_invalid_values(string query, int count)
    {
        var client = await f.AsAsync("anonymous");
        Assert.Equal(count, (await client.GetFromJsonAsync<SearchPage>($"/v1/listings?{query}&size=24"))!.Count);
    }

    [Theory]
    [InlineData("rent_asc")]
    [InlineData("rent_desc")]
    [InlineData("newest")]
    [InlineData("confirmed")]
    public async Task Every_sort_pages_through_all_listings_once(string sort)
    {
        var client = await f.AsAsync("anonymous");
        var ids = new List<string>();
        string? cursor = null;
        do
        {
            var page = await client.GetFromJsonAsync<SearchPage>($"/v1/listings?size=4&sort={sort}" + (cursor is null ? "" : $"&cursor={cursor}"));
            ids.AddRange(page!.Items.Select(c => c.ShortId));
            cursor = page.NextCursor;
        } while (cursor is not null);
        Assert.Equal(15, ids.Count);
        Assert.Equal(15, ids.Distinct().Count());
    }

    [Fact]
    public async Task Rent_sort_is_ordered_and_the_most_recently_confirmed_comes_first_by_default()
    {
        var client = await f.AsAsync("anonymous");
        var rents = (await client.GetFromJsonAsync<SearchPage>("/v1/listings?size=24&sort=rent_asc"))!.Items.Select(c => c.Rent).ToList();
        Assert.Equal(rents.Order(), rents);
        Assert.Equal("B1", (await client.GetFromJsonAsync<SearchPage>("/v1/listings?size=4"))!.Items[0].ShortId);
    }

    [Fact]
    public async Task Keyword_search_matches_amenity_names()
    {
        var client = await f.AsAsync("anonymous");
        var page = await client.GetFromJsonAsync<SearchPage>("/v1/listings?q=generator");
        Assert.True(page!.Count > 0);
        Assert.Equal(0, (await client.GetFromJsonAsync<SearchPage>("/v1/listings?q=swimmingpool"))!.Count);
    }
}
