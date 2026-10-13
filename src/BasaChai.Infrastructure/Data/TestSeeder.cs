using System.Text.Json;
using System.Text.Json.Serialization;
using BasaChai.Application.Common;
using BasaChai.Application.Identity;
using BasaChai.Application.Listings;
using BasaChai.Domain;
using BasaChai.Domain.Listings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BasaChai.Infrastructure.Data;

public sealed class SeedOptions
{
    /// <summary>Path to <c>deploy/seed/pilot.json</c>.</summary>
    public string PilotFile { get; set; } = "";
    /// <summary>Shared password for every seeded user (the E2E_PASSWORD secret). Never committed.</summary>
    public string Password { get; set; } = "";
}

/// <summary>
/// Wipes everything and loads a named dataset, with times relative to <c>now</c>. Test and Staging only:
/// the API registers it, and the /v1/test hooks that call it, only in those environments.
/// </summary>
public sealed class TestSeeder(AppDbContext db, ReferenceCache refs, IAddressProtector addresses,
    IPasswordHasher<AppUser> hasher, IRateLimiter limiter, SeedOptions options, Services.HashSalts salts)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static string? _hashFor, _hash;

    public async Task SeedAsync(string dataset, DateTimeOffset now, CancellationToken ct = default)
    {
        if (dataset is not ("pilot" or "empty")) throw ValidationFailedException.For("dataset", "invalid");
        if (string.IsNullOrEmpty(options.Password)) throw new InvalidOperationException("Seed:Password (E2E_PASSWORD) is not set.");
        var pilot = JsonSerializer.Deserialize<PilotFile>(await File.ReadAllTextAsync(options.PilotFile, ct), JsonOptions)
                    ?? throw new InvalidOperationException("Pilot dataset is empty.");

        await db.Database.ExecuteSqlRawAsync("""
            TRUNCATE users, user_roles, user_claims, user_logins, user_tokens, password_reset_tokens,
                     property_listings, listing_charges, listing_photos, listing_amenities, saved_listings,
                     listing_reports, moderation_actions, listing_availability_history, audit_log,
                     contact_events, analytics_events, app_settings, hash_salts,
                     location_aliases, locations, property_types, amenities RESTART IDENTITY CASCADE
            """, ct);
        db.ChangeTracker.Clear();
        refs.Invalidate();
        limiter.Reset();
        salts.Forget();   // the wipe removed the stored salts too
        await ReferenceSeeder.SeedAsync(db, ct);

        var users = await SeedUsersAsync(pilot.Users, now, ct);
        if (dataset == "pilot") await SeedListingsAsync(pilot, users, now, ct);
        db.ChangeTracker.Clear();
    }

    private async Task<Dictionary<string, AppUser>> SeedUsersAsync(IReadOnlyList<PilotUser> list, DateTimeOffset now, CancellationToken ct)
    {
        // Hashing is slow by design; the same password is hashed once per run rather than once per user.
        if (_hashFor != options.Password) { _hash = hasher.HashPassword(new AppUser(), options.Password); _hashFor = options.Password; }
        var roles = await db.Roles.ToDictionaryAsync(r => r.Name!, r => r.Id, ct);
        var users = new Dictionary<string, AppUser>();
        foreach (var u in list)
        {
            var e164 = Phone.ToE164(u.Phone) ?? throw new InvalidOperationException($"Bad seed phone for {u.Key}.");
            var user = new AppUser
            {
                Id = DeterministicId(u.Key), UserName = e164, NormalizedUserName = e164, PhoneE164 = e164, DisplayName = u.Name,
                PasswordHash = _hash, SecurityStamp = Guid.NewGuid().ToString("N"), ConcurrencyStamp = Guid.NewGuid().ToString(),
                TwoFactorEnabled = u.TwoFactor, LockoutEnabled = false, CreatedAt = now.AddDays(-90),
            };
            db.Users.Add(user);
            foreach (var role in u.Roles) db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = roles[role] });
            if (u.TwoFactor)
                db.UserTokens.Add(new IdentityUserToken<Guid>
                {
                    UserId = user.Id, LoginProvider = "[AspNetUserStore]", Name = "AuthenticatorKey",
                    Value = Base32Secret(user.Id),
                });
            users[u.Key] = user;
        }
        await db.SaveChangesAsync(ct);
        return users;
    }

    private async Task SeedListingsAsync(PilotFile pilot, Dictionary<string, AppUser> users, DateTimeOffset now, CancellationToken ct)
    {
        var r = await refs.GetAsync(db, ct);
        var byShortId = new Dictionary<string, Listing>();
        foreach (var p in pilot.Listings)
        {
            var created = p.CreatedDaysAgo is { } cd ? now.AddDays(-cd) : now.AddHours(-(p.SubmittedHoursAgo ?? 0) - 24);
            var l = new Listing
            {
                ShortId = p.Id, OwnerId = users[p.Owner].Id, CreatedAt = created,
                ViewsCount = p.Views, RevealsCount = p.Reveals,
            };
            ListingEditor.Apply(l, new ListingInput(
                Title: p.Title, Type: p.Type, Area: p.Area, Bedrooms: p.Beds, Bathrooms: p.Baths, FloorAreaSqft: p.Sqft,
                FloorNumber: p.Floor, Furnishing: p.Furnish, Occupancy: p.Occ,
                AvailableFrom: p.AvailInDays > 0 ? DateOnly.FromDateTime(now.AddDays(p.AvailInDays).UtcDateTime) : null,
                Description: p.Description ?? "", Rules: p.Rules ?? "", FullAddress: p.FullAddress,
                MonthlyRent: p.Rent, ServiceCharge: p.Service,
                Advance: p.Status == "draft" ? null : new AdvanceInput(p.Advance, p.Advance is null),
                Others: p.Others.Select(o => new OtherCostInput(o.Label, o.Amount, o.Period)).ToList(),
                Amenities: p.Amenities), r, addresses);
            l.Photos = p.Photos.Select((photo, i) => new ListingPhoto
            {
                ListingId = l.Id, CloudinaryPublicId = "seed/" + photo, Version = 1, Width = 1280, Height = 960,
                Bytes = 250_000, SortOrder = i, CreatedAt = created,
            }).ToList();

            var status = ListingStatusNames.Parse(p.Status);
            DateTimeOffset? confirmed = p.ConfirmedDaysAgo is { } days ? now.AddDays(-days) : null;
            DateTimeOffset? submitted = p.SubmittedHoursAgo is { } h ? now.AddHours(-h) : confirmed?.AddDays(-1);
            l.SeedState(status, submitted, confirmed, p.Reason);
            l.UpdatedAt = confirmed ?? submitted ?? created;
            db.Listings.Add(l);
            byShortId[p.Id] = l;
        }
        foreach (var rep in pilot.Reports)
            db.Reports.Add(new ListingReport
            {
                ListingId = byShortId[rep.Listing].Id, Reason = rep.Reason, Details = rep.Details,
                ReporterIpHash = "seed", CreatedAt = now.AddHours(-rep.HoursAgo),
            });
        if (pilot.HomeCostExample is { } example) db.Settings.Add(new AppSetting { Key = AppSetting.HomeCostExample, Value = example });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Stable ids so tests can refer to seeded users across resets.</summary>
    public static Guid DeterministicId(string key)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes("basachai-seed-user:" + key));
        return new Guid(bytes);
    }

    /// <summary>A fixed authenticator secret per seeded staff user, so test tools can compute codes.</summary>
    public static string Base32Secret(Guid id)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = id.ToByteArray();
        return string.Concat(bytes.Take(16).Select(b => alphabet[b % 32]));
    }

    private sealed record PilotFile(string? HomeCostExample, IReadOnlyList<PilotUser> Users, IReadOnlyList<PilotListing> Listings, IReadOnlyList<PilotReport> Reports);
    private sealed record PilotUser(string Key, string Name, string Phone, string[] Roles, bool TwoFactor);
    private sealed record PilotOther(string Label, int? Amount, string Period);
    private sealed record PilotListing(
        string Id, string Owner, string Type, string Area, string Title, string? Description, string? Rules,
        int Rent, int Service, int? Advance, IReadOnlyList<PilotOther> Others, int Beds, int Baths, int? Sqft, int? Floor,
        string Furnish, string Occ, int AvailInDays, IReadOnlyList<string> Amenities, string Status,
        double? ConfirmedDaysAgo, double? SubmittedHoursAgo, double? CreatedDaysAgo, string? Reason, int Views, int Reveals,
        IReadOnlyList<string> Photos, string? FullAddress);
    private sealed record PilotReport(string Id, string Listing, string Reason, double HoursAgo, string? Details);
}
