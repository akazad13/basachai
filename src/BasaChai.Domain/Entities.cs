using System.Text.Json;
using BasaChai.Domain.Listings;

namespace BasaChai.Domain;

// ---------- Reference data ----------

public class Location
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    /// <summary>ltree path, for example <c>dhaka.dhaka_district.dhamrai.bazar</c>.</summary>
    public string Path { get; set; } = "";
    public string Kind { get; set; } = "area";
    public string NameBn { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<LocationAlias> Aliases { get; set; } = [];
}

public class LocationAlias
{
    public int Id { get; set; }
    public int LocationId { get; set; }
    public string Alias { get; set; } = "";
    public string Normalized { get; set; } = "";
}

public class PropertyType
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string NameBn { get; set; } = "";
    public string NameEn { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class Amenity
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string NameBn { get; set; } = "";
    public string NameEn { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

// ---------- People and saved homes ----------

public class SavedListing
{
    public Guid UserId { get; set; }
    public Guid ListingId { get; set; }
    public Listing Listing { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}

public class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public Guid IssuedBy { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

// ---------- Moderation, audit and analytics ----------

public static class ReportReasons
{
    public static readonly string[] All = ["suspicious", "inaccurate", "duplicate", "unavailable"];
}

public class ListingReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ListingId { get; set; }
    public Listing Listing { get; set; } = null!;
    public Guid? ReporterId { get; set; }
    public string ReporterIpHash { get; set; } = "";
    public string Reason { get; set; } = "";
    public string? Details { get; set; }
    /// <summary><c>open</c>, <c>hidden</c> or <c>dismissed</c>.</summary>
    public string Status { get; set; } = "open";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedBy { get; set; }
}

public class ModerationAction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ListingId { get; set; }
    public Guid? ReportId { get; set; }
    public Guid ModeratorId { get; set; }
    public string Action { get; set; } = "";
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class AvailabilityHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ListingId { get; set; }
    public Listing Listing { get; set; } = null!;
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public Guid? ActorId { get; set; }
    public string ActorType { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public class AuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public JsonDocument? Before { get; set; }
    public JsonDocument? After { get; set; }
    public string? IpHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class ContactEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ListingId { get; set; }
    public Listing Listing { get; set; } = null!;
    public Guid? UserId { get; set; }
    public string DeviceHash { get; set; } = "";
    public string IpHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public class AnalyticsEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? SessionId { get; set; }
    public Guid? UserId { get; set; }
    public JsonDocument Props { get; set; } = JsonDocument.Parse("{}");
    public DateTimeOffset CreatedAt { get; set; }
}
