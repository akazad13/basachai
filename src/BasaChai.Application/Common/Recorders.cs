using System.Text.Json;
using BasaChai.Domain;
using BasaChai.Domain.Listings;

namespace BasaChai.Application.Common;

public static class Json
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    public static JsonDocument Doc(object value) => JsonSerializer.SerializeToDocument(value, Web);
}

/// <summary>Writes audit rows, analytics events and status history in the caller's unit of work.</summary>
public static class Recorders
{
    public static void Audit(this IAppDbContext db, DateTimeOffset now, Guid? actorId, string action, string entityType,
        string entityId, object? before = null, object? after = null, string? ipHash = null)
        => db.AuditLog.Add(new AuditEntry
        {
            ActorId = actorId, Action = action, EntityType = entityType, EntityId = entityId,
            Before = before is null ? null : Json.Doc(before), After = after is null ? null : Json.Doc(after),
            IpHash = ipHash, CreatedAt = now,
        });

    /// <summary>Records an analytics event. Callers pass only ids, counts and keys: never phones, addresses or free text.</summary>
    public static void Track(this IAppDbContext db, DateTimeOffset now, string name, object props, RequestContext? ctx)
        => db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            Name = name, Props = Json.Doc(props), UserId = ctx?.UserId, SessionId = ctx?.SessionId, CreatedAt = now,
        });

    /// <summary>Moves the listing's pending status changes into the history table, and logs read-time expiries.</summary>
    public static void FlushHistory(this IAppDbContext db, Listing listing)
    {
        foreach (var c in listing.PendingHistory)
        {
            db.AvailabilityHistory.Add(new AvailabilityHistory
            {
                ListingId = listing.Id, FromStatus = c.From.ToValue(), ToStatus = c.To.ToValue(),
                ActorId = c.Actor.Id, ActorType = c.Actor.Type.ToValue(), CreatedAt = c.At,
            });
            if (c.To == ListingStatus.Expired && c.From != ListingStatus.Expired)
                db.Track(c.At, "listing_expired", new { listingId = listing.ShortId }, null);
        }
        listing.ClearPendingHistory();
    }
}
