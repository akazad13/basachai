using BasaChai.Domain.Listings;

namespace BasaChai.Domain.Tests;

public class ListingLifecycleTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-10-13T06:00:00Z");

    [Fact] // LIFE-UT01
    public void LIFE_UT01_Live_listing_past_expiry_reads_as_expired()
    {
        var listing = ListingBuilder.Live(confirmedAt: T0);
        Assert.Equal(ListingStatus.Published, listing.EffectiveStatus(T0.AddDays(30).AddMinutes(-1)));
        Assert.Equal(ListingStatus.Expired, listing.EffectiveStatus(T0.AddDays(30).AddMinutes(1)));
    }

    [Fact] // LIFE-UT02
    public void LIFE_UT02_Resuming_after_expiry_expires_instead()
    {
        var listing = ListingBuilder.Live(confirmedAt: T0);
        listing.Pause(T0.AddDays(5));
        listing.Resume(T0.AddDays(31));
        Assert.Equal(ListingStatus.Expired, listing.Status);
    }

    public static TheoryData<ListingStatus, ListingAction, bool> Matrix => TransitionMatrix.All();

    [Theory, MemberData(nameof(Matrix))] // LIFE-UT03
    public void LIFE_UT03_Only_listed_transitions_are_allowed(ListingStatus from, ListingAction action, bool allowed)
    {
        var listing = ListingBuilder.InStatus(from, T0);
        var act = () => listing.Apply(action, ActorFor(action), T0, reason: "Reason");
        if (allowed) act(); else Assert.Throws<InvalidTransitionException>(act);
    }

    [Fact] // LIFE-UT04
    public void LIFE_UT04_Approve_and_reconfirm_set_thirty_days()
    {
        var listing = ListingBuilder.InStatus(ListingStatus.Pending, T0);
        listing.Approve(moderatorId: Guid.NewGuid(), T0);
        Assert.Equal(T0.AddDays(30), listing.ExpiresAt);
        listing.Reconfirm(T0.AddDays(10));
        Assert.Equal(T0.AddDays(40), listing.ExpiresAt);
    }

    [Fact]
    public void Read_time_expiry_is_written_to_history_once()
    {
        var listing = ListingBuilder.Live(confirmedAt: T0);
        listing.ClearPendingHistory();
        Assert.True(listing.ExpireIfDue(T0.AddDays(31)));
        Assert.False(listing.ExpireIfDue(T0.AddDays(32)));
        var change = Assert.Single(listing.PendingHistory);
        Assert.Equal((ListingStatus.Published, ListingStatus.Expired, ActorType.System), (change.From, change.To, change.Actor.Type));
    }

    [Fact]
    public void Submit_with_two_photos_is_refused_and_stays_draft()
    {
        var listing = ListingBuilder.InStatus(ListingStatus.Draft, T0, photos: 2);
        var ex = Assert.Throws<PhotosRequiredException>(() => listing.Submit(T0));
        Assert.Equal("photos_required", ex.Code);
        Assert.Equal(ListingStatus.Draft, listing.Status);
    }

    [Fact]
    public void Hide_keeps_status_and_expiry()
    {
        var listing = ListingBuilder.Live(confirmedAt: T0);
        var expires = listing.ExpiresAt;
        listing.Hide(T0.AddDays(1));
        Assert.False(listing.IsPublic(T0.AddDays(1)));
        listing.Unhide();
        Assert.Equal((ListingStatus.Published, expires), (listing.Status, listing.ExpiresAt));
        Assert.True(listing.IsPublic(T0.AddDays(1)));
    }

    [Theory]
    [InlineData(ListingAction.RequestChanges)]
    [InlineData(ListingAction.Reject)]
    public void Declining_needs_a_reason(ListingAction action)
    {
        var listing = ListingBuilder.InStatus(ListingStatus.Pending, T0);
        Assert.Throws<ValidationFailedException>(() => listing.Apply(action, ActorFor(action), T0, reason: " "));
        Assert.Equal(ListingStatus.Pending, listing.Status);
    }

    private static Actor ActorFor(ListingAction action) => action switch
    {
        ListingAction.Approve or ListingAction.RequestChanges or ListingAction.Reject => Actor.Moderator(Guid.NewGuid()),
        _ => Actor.Owner(Guid.NewGuid()),
    };
}
