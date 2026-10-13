using System.Security.Cryptography;
using BasaChai.Api.Http;
using BasaChai.Application.Common;
using BasaChai.Application.Identity;
using BasaChai.Application.Listings;
using BasaChai.Application.Moderation;
using BasaChai.Domain;
using BasaChai.Domain.Listings;
using BasaChai.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BasaChai.Api.Endpoints;

public sealed record ReasonBody(string? Reason);
public sealed record ResolveBody(string? Action);
public sealed record CostExampleBody(string? ShortId);
public sealed record ConfigItemBody(string? Key, string? NameBn, string? NameEn, bool? IsActive, string? Parent, string[]? Aliases);
public sealed record AdminUserDto(Guid Id, string Name, string Phone, string Status, IReadOnlyList<string> Roles, DateTimeOffset CreatedAt, int Listings);
public sealed record ResetLinkDto(string Token, string Link, DateTimeOffset ExpiresAt);
/// <summary>NearLimit: 80% of the monthly credits, the roadmap's trigger to plan a move off the free plan.</summary>
public sealed record PhotoUsageDto(bool Available, bool NearLimit, PhotoUsage? Usage);

/// <summary>Moderation for moderators and admins; users, configuration and audit for admins only. Staff need two-factor sign-in.</summary>
public static class AdminEndpoints
{
    public const string StaffPolicy = "staff";
    public const string AdminPolicy = "admin";

    public static void MapAdmin(this IEndpointRouteBuilder app)
    {
        var staff = app.MapGroup("/v1/admin").RequireAuthorization(StaffPolicy).AddEndpointFilter(RequireTwoFactor);
        var admin = staff.MapGroup("").RequireAuthorization(AdminPolicy);

        staff.MapGet("/stats", async (ModerationService svc) => Results.Ok(await svc.StatsAsync()));

        // Fetched when the admin dashboard opens (photos spec, Free-plan budget). A Cloudinary outage must not break the page.
        admin.MapGet("/photo-usage", async (IPhotoStore photos, ILoggerFactory logs, CancellationToken ct) =>
        {
            // A slow Cloudinary must not hold the dashboard: give it 5 seconds.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                var usage = await photos.UsageAsync(timeout.Token);
                return Results.Ok(new PhotoUsageDto(usage is not null, usage?.UsedPercent >= 80, usage));
            }
            catch (Exception e) when (!ct.IsCancellationRequested)   // the SDK's own timeouts are cancellations too
            {
                logs.CreateLogger("PhotoUsage").LogWarning(e, "Could not read the photo service usage");
                return Results.Ok(new PhotoUsageDto(false, false, null));
            }
        });

        staff.MapGet("/listings", async (string? status, string? q, string? area, Guid? owner, ModerationService svc)
            => Results.Ok(await svc.ListingsAsync(status, q, area, owner)));

        staff.MapGet("/listings/{key}/history", async (string key, ModerationService svc) => Results.Ok(await svc.HistoryAsync(key)));

        staff.MapPost("/listings/{key}/approve", async (string key, HttpContext http, ModerationService svc) =>
        {
            await svc.ApproveAsync(http.User.RequiredUserId(), key);
            return Results.NoContent();
        });
        staff.MapPost("/listings/{key}/request-changes", async (string key, ReasonBody body, HttpContext http, ModerationService svc) =>
        {
            await svc.DeclineAsync(http.User.RequiredUserId(), key, "request-changes", body.Reason);
            return Results.NoContent();
        });
        staff.MapPost("/listings/{key}/reject", async (string key, ReasonBody body, HttpContext http, ModerationService svc) =>
        {
            await svc.DeclineAsync(http.User.RequiredUserId(), key, "reject", body.Reason);
            return Results.NoContent();
        });
        staff.MapPost("/listings/{key}/hide", async (string key, HttpContext http, ModerationService svc) =>
        {
            await svc.HideAsync(http.User.RequiredUserId(), key);
            return Results.NoContent();
        });
        staff.MapPost("/listings/{key}/unhide", async (string key, HttpContext http, ModerationService svc) =>
        {
            await svc.UnhideAsync(http.User.RequiredUserId(), key);
            return Results.NoContent();
        });

        staff.MapGet("/reports", async (ModerationService svc) => Results.Ok(await svc.OpenReportsAsync()));
        staff.MapPost("/reports/{id:guid}/resolve", async (Guid id, ResolveBody body, HttpContext http, ModerationService svc) =>
        {
            await svc.ResolveReportAsync(http.User.RequiredUserId(), id, body.Action ?? "");
            return Results.NoContent();
        });

        // ---------- Admin only ----------

        admin.MapGet("/users", async (string? q, UserManager<AppUser> users, IAppDbContext db) =>
        {
            var query = users.Users.Where(u => u.DeletedAt == null);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var phone = Phone.ToE164(q);
                query = query.Where(u => u.DisplayName.Contains(q.Trim()) || (phone != null && u.PhoneE164 == phone));
            }
            var list = await query.OrderBy(u => u.DisplayName).Take(100).ToListAsync();
            var ids = list.Select(u => u.Id).ToList();
            var counts = await db.Listings.Where(l => ids.Contains(l.OwnerId)).GroupBy(l => l.OwnerId).Select(g => new { g.Key, N = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.N);
            var result = new List<AdminUserDto>();
            foreach (var u in list)
                result.Add(new AdminUserDto(u.Id, u.DisplayName, u.PhoneE164, u.Status, (await users.GetRolesAsync(u)).Order().ToList(), u.CreatedAt, counts.GetValueOrDefault(u.Id)));
            return Results.Ok(result);
        });

        foreach (var (path, status) in new[] { ("restrict", UserStatus.Restricted), ("suspend", UserStatus.Suspended), ("reinstate", UserStatus.Active) })
            admin.MapPost($"/users/{{id:guid}}/{path}", async (Guid id, HttpContext http, UserManager<AppUser> users, IAppDbContext db, TimeProvider clock) =>
            {
                var user = await users.FindByIdAsync(id.ToString()) ?? throw new NotFoundException();
                if (user.Id == http.User.RequiredUserId()) throw ValidationFailedException.For("user", "not_self");
                var before = user.Status;
                await using var tx = await db.Database.BeginTransactionAsync();
                user.Status = status;
                await users.UpdateAsync(user);
                if (status == UserStatus.Suspended) await users.UpdateSecurityStampAsync(user);   // ends their sessions now
                db.Audit(clock.GetUtcNow(), http.User.RequiredUserId(), "user." + path, "user", user.Id.ToString(), new { status = before }, new { status });
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.NoContent();
            });

        admin.MapPost("/users/{id:guid}/issue-password-reset", async (Guid id, HttpContext http, UserManager<AppUser> users,
            IAppDbContext db, TimeProvider clock, IConfiguration config) =>
        {
            var user = await users.FindByIdAsync(id.ToString()) ?? throw new NotFoundException();
            if (user.DeletedAt is not null) throw new NotFoundException();
            var now = clock.GetUtcNow();
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
            // Older unused links for this user stop working when a new one is issued.
            await db.PasswordResetTokens.Where(t => t.UserId == user.Id && t.ConsumedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumedAt, now));
            var expires = now.AddHours(24);
            db.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user.Id, TokenHash = AccountEndpoints.HashToken(token), IssuedBy = http.User.RequiredUserId(), ExpiresAt = expires,
            });
            db.Audit(now, http.User.RequiredUserId(), "password_reset.issue", "user", user.Id.ToString());
            await db.SaveChangesAsync();
            var web = config["Web:Origin"]?.TrimEnd('/') ?? "";
            return Results.Ok(new ResetLinkDto(token, $"{web}/{user.PreferredLocale}/reset?token={token}", expires));
        });

        admin.MapGet("/audit", async (Guid? actor, string? action, string? entity, string? entityId, DateTimeOffset? from, DateTimeOffset? to, IAppDbContext db) =>
        {
            var q = db.AuditLog.AsNoTracking();
            if (actor is { } a) q = q.Where(x => x.ActorId == a);
            if (!string.IsNullOrWhiteSpace(action)) q = q.Where(x => x.Action.StartsWith(action));
            if (!string.IsNullOrWhiteSpace(entity)) q = q.Where(x => x.EntityType == entity);
            if (!string.IsNullOrWhiteSpace(entityId)) q = q.Where(x => x.EntityId == entityId);
            // PostgreSQL timestamptz parameters must be UTC.
            if (from is { } f) { var fu = f.ToUniversalTime(); q = q.Where(x => x.CreatedAt >= fu); }
            if (to is { } t) { var tu = t.ToUniversalTime(); q = q.Where(x => x.CreatedAt <= tu); }
            var rows = await q.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync();
            return Results.Ok(rows.Select(x => new
            {
                x.Id, x.ActorId, x.Action, x.EntityType, x.EntityId, x.CreatedAt,
                before = x.Before?.RootElement.Clone(), after = x.After?.RootElement.Clone(),
            }));
        });

        admin.MapPut("/settings/home-cost-example", async (CostExampleBody body, HttpContext http, IAppDbContext db, TimeProvider clock) =>
        {
            var shortId = body.ShortId?.Trim();
            if (string.IsNullOrEmpty(shortId) || !await db.Listings.AnyAsync(l => l.ShortId == shortId)) throw ValidationFailedException.For("listing", "invalid");
            var setting = await db.Settings.FindAsync(AppSetting.HomeCostExample);
            if (setting is null) db.Settings.Add(new AppSetting { Key = AppSetting.HomeCostExample, Value = shortId });
            else setting.Value = shortId;
            db.Audit(clock.GetUtcNow(), http.User.RequiredUserId(), "settings.home_cost_example", "setting", AppSetting.HomeCostExample, null, new { shortId });
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        MapConfig(admin);
    }

    /// <summary>Areas, property types and amenities. Items are deactivated rather than deleted so old listings keep their names.</summary>
    private static void MapConfig(RouteGroupBuilder admin)
    {
        admin.MapGet("/property-types", async (IAppDbContext db) => Results.Ok(await db.PropertyTypes.OrderBy(t => t.Id).ToListAsync()));
        admin.MapGet("/amenities", async (IAppDbContext db) => Results.Ok(await db.Amenities.OrderBy(a => a.Id).ToListAsync()));
        admin.MapGet("/locations", async (IAppDbContext db) => Results.Ok(await db.Locations.Include(l => l.Aliases).OrderBy(l => l.Id).ToListAsync()));

        admin.MapPost("/property-types", async (ConfigItemBody b, HttpContext http, IAppDbContext db, ReferenceCache refs, TimeProvider clock) =>
        {
            var (key, bn, en) = Required(b);
            if (await db.PropertyTypes.AnyAsync(t => t.Key == key)) throw Errors.Conflict("key_taken", "That key is already used.");
            var item = new PropertyType { Key = key, NameBn = bn, NameEn = en, IsActive = b.IsActive ?? true };
            db.PropertyTypes.Add(item);
            return await SaveConfig(db, refs, clock, http, "config.property_type.create", key, item);
        });
        admin.MapPost("/amenities", async (ConfigItemBody b, HttpContext http, IAppDbContext db, ReferenceCache refs, TimeProvider clock) =>
        {
            var (key, bn, en) = Required(b);
            if (await db.Amenities.AnyAsync(a => a.Key == key)) throw Errors.Conflict("key_taken", "That key is already used.");
            var item = new Amenity { Key = key, NameBn = bn, NameEn = en, IsActive = b.IsActive ?? true };
            db.Amenities.Add(item);
            return await SaveConfig(db, refs, clock, http, "config.amenity.create", key, item);
        });
        admin.MapPost("/locations", async (ConfigItemBody b, HttpContext http, IAppDbContext db, ReferenceCache refs, TimeProvider clock) =>
        {
            var (key, bn, en) = Required(b);
            var parent = await db.Locations.FirstOrDefaultAsync(l => l.Slug == (b.Parent ?? "dhamrai")) ?? throw ValidationFailedException.For("parent", "invalid");
            if (await db.Locations.AnyAsync(l => l.Slug == key)) throw Errors.Conflict("key_taken", "That key is already used.");
            var item = new Location { ParentId = parent.Id, Path = $"{parent.Path}.{key.Replace('-', '_')}", Kind = "area", Slug = key, NameBn = bn, NameEn = en, IsActive = b.IsActive ?? true };
            foreach (var alias in (b.Aliases ?? []).Append(bn).Append(en)) item.Aliases.Add(new LocationAlias { Alias = alias, Normalized = ReferenceSeeder.Normalize(alias) });
            db.Locations.Add(item);
            return await SaveConfig(db, refs, clock, http, "config.location.create", key, new { item.Slug, item.NameBn, item.NameEn });
        });

        admin.MapPatch("/property-types/{key}", async (string key, ConfigItemBody b, HttpContext http, IAppDbContext db, ReferenceCache refs, TimeProvider clock) =>
        {
            var item = await db.PropertyTypes.FirstOrDefaultAsync(t => t.Key == key) ?? throw new NotFoundException();
            (item.NameBn, item.NameEn, item.IsActive) = (b.NameBn ?? item.NameBn, b.NameEn ?? item.NameEn, b.IsActive ?? item.IsActive);
            return await SaveConfig(db, refs, clock, http, "config.property_type.update", key, item);
        });
        admin.MapPatch("/amenities/{key}", async (string key, ConfigItemBody b, HttpContext http, IAppDbContext db, ReferenceCache refs, TimeProvider clock) =>
        {
            var item = await db.Amenities.FirstOrDefaultAsync(a => a.Key == key) ?? throw new NotFoundException();
            (item.NameBn, item.NameEn, item.IsActive) = (b.NameBn ?? item.NameBn, b.NameEn ?? item.NameEn, b.IsActive ?? item.IsActive);
            return await SaveConfig(db, refs, clock, http, "config.amenity.update", key, item);
        });
        admin.MapPatch("/locations/{slug}", async (string slug, ConfigItemBody b, HttpContext http, IAppDbContext db, ReferenceCache refs, TimeProvider clock) =>
        {
            var item = await db.Locations.Include(l => l.Aliases).FirstOrDefaultAsync(l => l.Slug == slug) ?? throw new NotFoundException();
            (item.NameBn, item.NameEn, item.IsActive) = (b.NameBn ?? item.NameBn, b.NameEn ?? item.NameEn, b.IsActive ?? item.IsActive);
            if (b.Aliases is { } aliases)
            {
                item.Aliases.Clear();
                foreach (var alias in aliases.Append(item.NameBn).Append(item.NameEn).Distinct())
                    item.Aliases.Add(new LocationAlias { Alias = alias, Normalized = ReferenceSeeder.Normalize(alias) });
            }
            return await SaveConfig(db, refs, clock, http, "config.location.update", slug, new { item.Slug, item.NameBn, item.NameEn, item.IsActive });
        });

        foreach (var kind in new[] { "property-types", "amenities", "locations" })
            admin.MapDelete($"/{kind}/{{key}}", async (string key, HttpContext http, IAppDbContext db, ReferenceCache refs, TimeProvider clock) =>
            {
                switch (kind)
                {
                    case "property-types": (await db.PropertyTypes.FirstOrDefaultAsync(t => t.Key == key) ?? throw new NotFoundException()).IsActive = false; break;
                    case "amenities": (await db.Amenities.FirstOrDefaultAsync(a => a.Key == key) ?? throw new NotFoundException()).IsActive = false; break;
                    default: (await db.Locations.FirstOrDefaultAsync(l => l.Slug == key) ?? throw new NotFoundException()).IsActive = false; break;
                }
                await SaveConfig(db, refs, clock, http, $"config.{kind}.deactivate", key, null);
                return Results.NoContent();
            });
    }

    private static (string Key, string Bn, string En) Required(ConfigItemBody b)
    {
        var key = b.Key?.Trim().ToLowerInvariant() ?? "";
        var e = new Dictionary<string, string[]>();
        if (key.Length is < 2 or > 40 || !key.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '-' or '_')) e["key"] = ["invalid"];
        if (string.IsNullOrWhiteSpace(b.NameBn)) e["nameBn"] = ["required"];
        if (string.IsNullOrWhiteSpace(b.NameEn)) e["nameEn"] = ["required"];
        if (e.Count > 0) throw new ValidationFailedException(e);
        return (key, b.NameBn!.Trim(), b.NameEn!.Trim());
    }

    private static async Task<IResult> SaveConfig(IAppDbContext db, ReferenceCache refs, TimeProvider clock, HttpContext http, string action, string key, object? after)
    {
        db.Audit(clock.GetUtcNow(), http.User.RequiredUserId(), action, "config", key, null, after);
        await db.SaveChangesAsync();
        refs.Invalidate();
        return after is null ? Results.NoContent() : Results.Ok(after);
    }

    /// <summary>AUTH-7: staff without two-factor sign-in are sent to set it up before using staff pages.</summary>
    private static async ValueTask<object?> RequireTwoFactor(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var users = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
        var user = await users.GetUserAsync(ctx.HttpContext.User);
        if (user is null || !user.TwoFactorEnabled)
            throw new ForbiddenException("two_factor_required", "Set up two-factor sign-in before using staff pages.");
        return await next(ctx);
    }
}
