using BasaChai.Application.Identity;
using BasaChai.Domain;
using BasaChai.Domain.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BasaChai.Application.Common;

public interface IAppDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<Listing> Listings { get; }
    DbSet<ListingCharge> Charges { get; }
    DbSet<ListingPhoto> Photos { get; }
    DbSet<ListingAmenity> ListingAmenities { get; }
    DbSet<Location> Locations { get; }
    DbSet<LocationAlias> LocationAliases { get; }
    DbSet<PropertyType> PropertyTypes { get; }
    DbSet<Amenity> Amenities { get; }
    DbSet<SavedListing> Saved { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<ListingReport> Reports { get; }
    DbSet<ModerationAction> ModerationActions { get; }
    DbSet<AvailabilityHistory> AvailabilityHistory { get; }
    DbSet<AuditEntry> AuditLog { get; }
    DbSet<ContactEvent> ContactEvents { get; }
    DbSet<AnalyticsEvent> AnalyticsEvents { get; }
    DbSet<AppSetting> Settings { get; }
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Small key-value settings an admin can change, such as the home page cost example.</summary>
public class AppSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";

    public const string HomeCostExample = "home.cost_example";
}

/// <summary>Where listing photos live. Cloudinary in production; a hand-written fake in tests.</summary>
public interface IPhotoStore
{
    Task<StoredPhoto> UploadAsync(Stream content, string folder, CancellationToken ct = default);
    Task DeleteAsync(string publicId, CancellationToken ct = default);

    /// <summary>This month's use of the photo service, for the admin dashboard; null when the store cannot tell.</summary>
    Task<PhotoUsage?> UsageAsync(CancellationToken ct = default) => Task.FromResult<PhotoUsage?>(null);
}

public sealed record StoredPhoto(string PublicId, long Version, int Width, int Height, long Bytes);

/// <summary>Cloudinary meters storage, bandwidth and transformations against a monthly credit allowance (photos spec).</summary>
public sealed record PhotoUsage(
    string? Plan, double CreditsUsed, double CreditsLimit, double UsedPercent,
    long StorageBytes, long BandwidthBytes, long Transformations, DateTimeOffset UpdatedAt);

public sealed class PhotoStoreException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Encrypts the private full address (ASP.NET Core Data Protection).</summary>
public interface IAddressProtector
{
    byte[] Protect(string address);
    string Unprotect(byte[] data);
}

/// <summary>
/// The web app's edge-cached pages (home, area and property pages, 60 seconds). After a change that a visitor could
/// see, the API asks the web app to drop the affected pages at once (content-and-localization spec, SEO).
/// Never throws: a failed purge only means the page refreshes within 60 seconds.
/// </summary>
public interface IPageCache
{
    /// <param name="listingShortIds">Listings whose public view changed (their pages and every listing page).</param>
    /// <param name="everything">Reference data or many listings changed: drop every cached page.</param>
    Task InvalidateAsync(IReadOnlyCollection<string> listingShortIds, bool everything = false);
}

/// <summary>Keyed hashes (HMAC-SHA256), so IPs and device ids cannot be recovered by trying guesses.</summary>
public interface IHasher
{
    /// <summary>Stable: for values never stored, such as rate-limit keys and cookie signatures.</summary>
    string Hash(string value);

    /// <summary>For IPs, devices and sessions written to the database: the salt rotates every month.</summary>
    string Stored(string value);
}

/// <summary>In-memory limits on the single API instance, timed by the app's TimeProvider so tests can move time.</summary>
public interface IRateLimiter
{
    /// <summary>Records a hit and returns null, or returns how long to wait when the limit is already reached.</summary>
    TimeSpan? TryHit(string bucket, string key, int limit, TimeSpan window);

    /// <summary>Like <see cref="TryHit"/> but records nothing: returns how long to wait if the limit is reached.</summary>
    TimeSpan? Check(string bucket, string key, int limit, TimeSpan window);

    /// <summary>Removes the most recent hit, for an attempt that turned out to be allowed (a successful login).</summary>
    void Refund(string bucket, string key);
    void Reset();
}

/// <summary>Who is calling, already hashed where needed.</summary>
/// <summary>
/// Who is calling. IpHash, DeviceHash and SessionId are the rotating hashes that may be stored; IpKey, DeviceKey and
/// SessionKey are stable and only for in-memory rate limits, which must not reset when the stored-hash salt rotates.
/// </summary>
public sealed record RequestContext(Guid? UserId, string IpHash, string DeviceHash, string? SessionId, IReadOnlyCollection<string> Roles)
{
    public string IpKey { get; init; } = IpHash;
    public string DeviceKey { get; init; } = DeviceHash;
    public string? SessionKey { get; init; } = SessionId;

    public bool IsStaff => Roles.Contains(Identity.Roles.Moderator) || Roles.Contains(Identity.Roles.Admin);
    public bool IsAdmin => Roles.Contains(Identity.Roles.Admin);
}

/// <summary>Turns the test clock in Test and Staging. Not registered in production.</summary>
public interface IClockControl
{
    void Advance(TimeSpan by);
    void Reset();
}
