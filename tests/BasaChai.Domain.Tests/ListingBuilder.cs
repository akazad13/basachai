using BasaChai.Domain.Listings;

namespace BasaChai.Domain.Tests;

/// <summary>Builds complete, valid listings in any status for state-machine tests.</summary>
public static class ListingBuilder
{
    public static Listing Live(DateTimeOffset confirmedAt) => InStatus(ListingStatus.Published, confirmedAt);

    public static Listing InStatus(ListingStatus status, DateTimeOffset at, int photos = 3)
    {
        var listing = new Listing
        {
            OwnerId = Guid.NewGuid(),
            ShortId = "T1",
            Title = "Two bedroom flat near the bazar",
            PropertyTypeId = 1,
            LocationId = 1,
            Bedrooms = 2,
            Bathrooms = 1,
            MonthlyRent = 10_500,
            ServiceCharge = 1_200,
            Charges = [new ListingCharge { Kind = ChargeKind.Advance, Amount = 21_000 }],
            Photos = Enumerable.Range(0, photos).Select(i => new ListingPhoto { CloudinaryPublicId = $"p{i}", SortOrder = i }).ToList(),
        };
        var live = status is ListingStatus.Published or ListingStatus.Paused;
        listing.SeedState(status, submittedAt: at, confirmedAt: live ? at : null, reason: null);
        return listing;
    }
}

/// <summary>The transitions table from the lifecycle spec, as data.</summary>
public static class TransitionMatrix
{
    private static readonly Dictionary<ListingAction, ListingStatus[]> Allowed = new()
    {
        [ListingAction.Submit] = [ListingStatus.Draft, ListingStatus.Changes],
        [ListingAction.Approve] = [ListingStatus.Pending],
        [ListingAction.RequestChanges] = [ListingStatus.Pending],
        [ListingAction.Reject] = [ListingStatus.Pending],
        [ListingAction.Reconfirm] = [ListingStatus.Published],
        [ListingAction.Pause] = [ListingStatus.Published],
        [ListingAction.Resume] = [ListingStatus.Paused],
        [ListingAction.MarkRented] = [ListingStatus.Published],
        [ListingAction.Renew] = [ListingStatus.Expired],
        [ListingAction.Archive] = Enum.GetValues<ListingStatus>().Except([ListingStatus.Pending, ListingStatus.Archived]).ToArray(),
    };

    public static TheoryData<ListingStatus, ListingAction, bool> All()
    {
        var data = new TheoryData<ListingStatus, ListingAction, bool>();
        foreach (var from in Enum.GetValues<ListingStatus>())
        foreach (var action in Enum.GetValues<ListingAction>())
            data.Add(from, action, Allowed[action].Contains(from));
        return data;
    }
}
