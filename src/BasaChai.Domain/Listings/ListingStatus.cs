namespace BasaChai.Domain.Listings;

public enum ListingStatus { Draft, Pending, Changes, Rejected, Published, Paused, Rented, Expired, Archived }

public enum ListingAction { Submit, Approve, RequestChanges, Reject, Reconfirm, Pause, Resume, MarkRented, Renew, Archive }

public enum ActorType { Owner, Moderator, System }

public sealed record Actor(ActorType Type, Guid? Id)
{
    public static readonly Actor System = new(ActorType.System, null);
    public static Actor Owner(Guid id) => new(ActorType.Owner, id);
    public static Actor Moderator(Guid id) => new(ActorType.Moderator, id);
}

/// <summary>A status change waiting to be written to listing_availability_history.</summary>
public sealed record StatusChange(ListingStatus From, ListingStatus To, Actor Actor, DateTimeOffset At);

public static class ListingStatusNames
{
    /// <summary>Stored and API value: lower case, matching the data model (<c>published</c>, <c>changes</c>, ...).</summary>
    public static string ToValue(this ListingStatus status) => status.ToString().ToLowerInvariant();

    public static ListingStatus Parse(string value)
        => Enum.TryParse<ListingStatus>(value, ignoreCase: true, out var s) && Enum.IsDefined(s)
            ? s
            : throw new ArgumentException($"Unknown listing status '{value}'.", nameof(value));

    public static bool TryParse(string? value, out ListingStatus status)
        => Enum.TryParse(value, ignoreCase: true, out status) && Enum.IsDefined(status) && !int.TryParse(value, out _);

    public static string ToValue(this ActorType type) => type.ToString().ToLowerInvariant();
}
