namespace BasaChai.Api.Tests;

/// <summary>
/// Saving a full form replaces a listing's charges and amenities. Found in the browser: EF treated the new charge
/// rows as existing ones and the save failed as a false stale_listing.
/// </summary>
public class EditGraphTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => f.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static object FullForm(int rent, string[] amenities) => new
    {
        Title = "Two bedroom flat by the market", Type = "flat", Area = "bazar", Bedrooms = 2, Bathrooms = 1,
        Furnishing = "unfurnished", Occupancy = "family", AvailableNow = true, Description = "", Rules = "", FullAddress = "",
        MonthlyRent = rent, ServiceCharge = 1_000, Advance = new { Amount = 24_000, NotSpecified = false },
        Others = new[] { new { Label = "Gas bill", Amount = (int?)1_080, Period = "monthly" }, new { Label = "Key deposit", Amount = (int?)1_000, Period = "one_time" } },
        Amenities = amenities,
    };

    [Fact]
    public async Task A_draft_can_be_saved_with_its_whole_form_again_and_again()
    {
        var owner = await f.AsAsync("owner");
        for (var i = 0; i < 3; i++)
        {
            var res = await owner.PatchAsJsonAsync("/v1/owner/listings/M1", FullForm(7_500 + i, ["gas", "balcony"]));
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
        var m1 = await owner.GetFromJsonAsync<OwnerListingDto>("/v1/owner/listings/M1");
        Assert.Equal((7_502, 2, 2), (m1!.MonthlyRent, m1.Others.Count, m1.Amenities.Count));
    }

    [Fact]
    public async Task A_live_listing_can_change_its_costs_and_amenities()
    {
        var owner = await f.AsAsync("owner");
        Assert.Equal(HttpStatusCode.OK, (await owner.PatchAsJsonAsync("/v1/owner/listings/Z1", FullForm(12_000, ["gas"]))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PatchAsJsonAsync("/v1/owner/listings/Z1", FullForm(12_000, ["gas", "lift"]))).StatusCode);
        var z1 = await owner.GetFromJsonAsync<OwnerListingDto>("/v1/owner/listings/Z1");
        Assert.Equal(["gas", "lift"], z1!.Amenities.Order());
        var anon = await f.AsAsync("anonymous");
        Assert.Equal(14_080, (await anon.GetFromJsonAsync<ListingDetailDto>("/v1/listings/Z1"))!.Card!.MonthlyTotal);
    }

    [Fact]
    public async Task Optional_numbers_can_be_emptied_and_unknown_ones_are_refused()
    {
        var owner = await f.AsAsync("owner");
        (await owner.PatchAsJsonAsync("/v1/owner/listings/M1", new { FloorAreaSqft = 900, FloorNumber = 3 })).EnsureSuccessStatusCode();
        (await owner.PatchAsJsonAsync("/v1/owner/listings/M1", new { Clear = new[] { "floorAreaSqft", "floorNumber" } })).EnsureSuccessStatusCode();
        var m1 = await owner.GetFromJsonAsync<OwnerListingDto>("/v1/owner/listings/M1");
        Assert.Null(m1!.FloorAreaSqft);
        Assert.Null(m1.FloorNumber);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PatchAsJsonAsync("/v1/owner/listings/M1", new { Clear = new[] { "monthlyRent" } })).StatusCode);
    }

    [Fact]
    public async Task Photos_can_be_added_to_a_listing_that_already_has_some()
    {
        var owner = await f.AsAsync("owner");
        var id = await owner.CreateDraftAsync(photos: 2);
        (await owner.PatchAsJsonAsync($"/v1/owner/listings/{id}", FullForm(8_000, []))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, (await owner.UploadPhotoAsync(id, TestFiles.SmallJpeg)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync($"/v1/owner/listings/{id}/submit", null)).StatusCode);
    }
}
