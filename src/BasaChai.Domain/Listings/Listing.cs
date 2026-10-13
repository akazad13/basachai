using BasaChai.Domain.Pricing;

namespace BasaChai.Domain.Listings;

/// <summary>
/// A property listing and its lifecycle. Only the transitions in the lifecycle spec are allowed;
/// anything else throws <see cref="InvalidTransitionException"/>. Expiry is applied at read time (no jobs).
/// </summary>
public class Listing
{
    public static readonly TimeSpan LiveFor = TimeSpan.FromDays(30);
    public const int MinPhotos = 3;
    public const int MaxPhotos = 20;

    private readonly List<StatusChange> _pendingHistory = [];

    public Guid Id { get; set; } = Guid.NewGuid();
    public string ShortId { get; set; } = "";
    public Guid OwnerId { get; set; }
    public string Purpose { get; set; } = "rent";
    public int? PropertyTypeId { get; set; }
    public int? LocationId { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public string? Rules { get; set; }
    public int? MonthlyRent { get; set; }
    public int? ServiceCharge { get; set; }
    public int Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public int? FloorAreaSqft { get; set; }
    public int? FloorNumber { get; set; }
    public string Furnishing { get; set; } = "unfurnished";
    public string Occupancy { get; set; } = "any";
    public DateOnly? AvailableFrom { get; set; }
    public byte[]? FullAddressEnc { get; set; }
    public string AdvertiserType { get; set; } = "owner";
    public ListingStatus Status { get; private set; } = ListingStatus.Draft;
    public bool IsHidden { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset? LastConfirmedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public string? ModeratorReason { get; private set; }
    public string? SaleDetails { get; set; }
    /// <summary>Amenity names in both scripts, kept for the full-text search vector.</summary>
    public string AmenityText { get; set; } = "";
    public int ViewsCount { get; set; }
    public int RevealsCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    /// <summary>Optimistic concurrency: bumped on every tracked change, not by the view and reveal counters.</summary>
    public int Version { get; set; }

    public List<ListingCharge> Charges { get; set; } = [];
    public List<ListingPhoto> Photos { get; set; } = [];
    public List<ListingAmenity> Amenities { get; set; } = [];

    /// <summary>Status changes made since load, for the history table. Cleared once saved.</summary>
    public IReadOnlyList<StatusChange> PendingHistory => _pendingHistory;
    public void ClearPendingHistory() => _pendingHistory.Clear();

    // ---------- Read-time rules ----------

    public ListingStatus EffectiveStatus(DateTimeOffset now)
        => Status == ListingStatus.Published && ExpiresAt is { } e && e <= now ? ListingStatus.Expired : Status;

    public bool IsPublic(DateTimeOffset now) => EffectiveStatus(now) == ListingStatus.Published && !IsHidden && Purpose == "rent";

    /// <summary>Stores the read-time expiry, with a system history row, if the 30 days have run out.</summary>
    public bool ExpireIfDue(DateTimeOffset now)
    {
        if (EffectiveStatus(now) != ListingStatus.Expired || Status == ListingStatus.Expired) return false;
        MoveTo(ListingStatus.Expired, Actor.System, now);
        return true;
    }

    public bool CanEdit(DateTimeOffset now)
        => EffectiveStatus(now) is ListingStatus.Draft or ListingStatus.Changes or ListingStatus.Published;

    public CostBreakdown Costs()
    {
        var others = Charges.Where(c => c.Kind != ChargeKind.Advance)
            .OrderBy(c => c.SortOrder)
            .Select(c => new OtherCost(c.Label, c.Amount, c.Kind == ChargeKind.OtherMonthly ? CostPeriod.Monthly : CostPeriod.OneTime))
            .ToList();
        var advance = Charges.FirstOrDefault(c => c.Kind == ChargeKind.Advance);
        return new CostBreakdown(MonthlyRent ?? 0, ServiceCharge ?? 0, others,
            advance?.Amount is { } a ? Advance.Of(a) : Advance.NotSpecified);
    }

    /// <summary>Fields still missing before the listing can be submitted, as field name to message key.</summary>
    public Dictionary<string, string[]> MissingForSubmit()
    {
        var e = new Dictionary<string, string[]>();
        if (Title.Trim().Length is < 10 or > 90) e["title"] = ["title_length"];
        if (PropertyTypeId is null) e["type"] = ["required"];
        if (LocationId is null) e["area"] = ["required"];
        if (Bedrooms is < 0 or > 10) e["bedrooms"] = ["required"];
        if (Bathrooms is null or < 1 or > 10) e["bathrooms"] = ["required"];
        if (MonthlyRent is null or <= 0) e["monthlyRent"] = ["required"];
        if (ServiceCharge is null or < 0) e["serviceCharge"] = ["required"];
        if (!Charges.Any(c => c.Kind == ChargeKind.Advance && (c.Amount is > 0 || c.NotSpecified))) e["advance"] = ["required"];
        return e;
    }

    // ---------- Transitions ----------

    public void Apply(ListingAction action, Actor actor, DateTimeOffset now, string? reason = null)
    {
        switch (action)
        {
            case ListingAction.Submit: Submit(now, actor); break;
            case ListingAction.Approve: Approve(actor.Id ?? Guid.Empty, now); break;
            case ListingAction.RequestChanges: RequestChanges(actor.Id ?? Guid.Empty, reason, now); break;
            case ListingAction.Reject: Reject(actor.Id ?? Guid.Empty, reason, now); break;
            case ListingAction.Reconfirm: Reconfirm(now, actor); break;
            case ListingAction.Pause: Pause(now, actor); break;
            case ListingAction.Resume: Resume(now, actor); break;
            case ListingAction.MarkRented: MarkRented(now, actor); break;
            case ListingAction.Renew: Renew(now, actor); break;
            case ListingAction.Archive: Archive(now, actor); break;
            default: throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    public void Submit(DateTimeOffset now, Actor? actor = null)
    {
        Require(ListingAction.Submit, now, ListingStatus.Draft, ListingStatus.Changes);
        var missing = MissingForSubmit();
        if (missing.Count > 0) throw new ValidationFailedException(missing);
        if (Photos.Count < MinPhotos) throw new PhotosRequiredException(Photos.Count);
        SubmittedAt = now;
        MoveTo(ListingStatus.Pending, actor ?? Actor.Owner(OwnerId), now);
    }

    public void Approve(Guid moderatorId, DateTimeOffset now)
    {
        Require(ListingAction.Approve, now, ListingStatus.Pending);
        PublishedAt ??= now;
        Confirm(now);
        ModeratorReason = null;
        MoveTo(ListingStatus.Published, Actor.Moderator(moderatorId), now);
    }

    public void RequestChanges(Guid moderatorId, string? reason, DateTimeOffset now)
        => Decline(ListingAction.RequestChanges, ListingStatus.Changes, moderatorId, reason, now);

    public void Reject(Guid moderatorId, string? reason, DateTimeOffset now)
        => Decline(ListingAction.Reject, ListingStatus.Rejected, moderatorId, reason, now);

    public void Reconfirm(DateTimeOffset now, Actor? actor = null)
    {
        Require(ListingAction.Reconfirm, now, ListingStatus.Published);
        Confirm(now);
        _pendingHistory.Add(new StatusChange(Status, Status, actor ?? Actor.Owner(OwnerId), now));
    }

    public void Pause(DateTimeOffset now, Actor? actor = null)
    {
        Require(ListingAction.Pause, now, ListingStatus.Published);
        MoveTo(ListingStatus.Paused, actor ?? Actor.Owner(OwnerId), now);
    }

    /// <summary>Back to Live, or to Expired if the 30 days ran out while paused.</summary>
    public void Resume(DateTimeOffset now, Actor? actor = null)
    {
        Require(ListingAction.Resume, now, ListingStatus.Paused);
        MoveTo(ExpiresAt > now ? ListingStatus.Published : ListingStatus.Expired, actor ?? Actor.Owner(OwnerId), now);
    }

    public void MarkRented(DateTimeOffset now, Actor? actor = null)
    {
        Require(ListingAction.MarkRented, now, ListingStatus.Published);
        MoveTo(ListingStatus.Rented, actor ?? Actor.Owner(OwnerId), now);
    }

    public void Renew(DateTimeOffset now, Actor? actor = null)
    {
        Require(ListingAction.Renew, now, ListingStatus.Expired);
        Confirm(now);
        MoveTo(ListingStatus.Published, actor ?? Actor.Owner(OwnerId), now);
    }

    public void Archive(DateTimeOffset now, Actor? actor = null)
    {
        ExpireIfDue(now);
        if (Status is ListingStatus.Pending or ListingStatus.Archived)
            throw new InvalidTransitionException(Status.ToValue(), nameof(ListingAction.Archive));
        MoveTo(ListingStatus.Archived, actor ?? Actor.Owner(OwnerId), now);
    }

    /// <summary>
    /// When an owner deletes their account every listing is archived by the system, including one in review,
    /// which an owner could not archive themselves: it must never go live under a deleted account.
    /// </summary>
    public void WithdrawForDeletedOwner(DateTimeOffset now)
    {
        if (Status == ListingStatus.Archived) return;
        MoveTo(ListingStatus.Archived, Actor.System, now);
    }

    /// <summary>Hide is a flag on a Live listing, not a status.</summary>
    public void Hide(DateTimeOffset now)
    {
        if (EffectiveStatus(now) != ListingStatus.Published || IsHidden)
            throw new InvalidTransitionException(Status.ToValue(), "hide");
        IsHidden = true;
    }

    public void Unhide()
    {
        if (!IsHidden) throw new InvalidTransitionException(Status.ToValue(), "unhide");
        IsHidden = false;
    }

    /// <summary>For the test seeder only: sets stored state directly, as if the history had happened.</summary>
    public void SeedState(ListingStatus status, DateTimeOffset? submittedAt, DateTimeOffset? confirmedAt, string? reason, bool hidden = false)
    {
        Status = status;
        SubmittedAt = submittedAt;
        LastConfirmedAt = confirmedAt;
        PublishedAt = confirmedAt;
        ExpiresAt = confirmedAt + LiveFor;
        ModeratorReason = reason;
        IsHidden = hidden;
    }

    private void Decline(ListingAction action, ListingStatus to, Guid moderatorId, string? reason, DateTimeOffset now)
    {
        Require(action, now, ListingStatus.Pending);
        var text = System.Text.RegularExpressions.Regex.Replace(reason ?? "", "<[^>]*>", "").Trim();
        if (text.Length == 0) throw ValidationFailedException.For("reason", "reason_err");
        if (text.Length > 500) throw ValidationFailedException.For("reason", "too_long");
        ModeratorReason = text;
        MoveTo(to, Actor.Moderator(moderatorId), now);
    }

    private void Confirm(DateTimeOffset now)
    {
        LastConfirmedAt = now;
        ExpiresAt = now + LiveFor;
    }

    private void Require(ListingAction action, DateTimeOffset now, params ListingStatus[] allowed)
    {
        ExpireIfDue(now);
        if (!allowed.Contains(Status)) throw new InvalidTransitionException(Status.ToValue(), action.ToString());
    }

    private void MoveTo(ListingStatus to, Actor actor, DateTimeOffset now)
    {
        _pendingHistory.Add(new StatusChange(Status, to, actor, now));
        Status = to;
    }
}
