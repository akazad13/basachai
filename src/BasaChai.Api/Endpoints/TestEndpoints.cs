using System.Text.Json;
using BasaChai.Application.Common;
using BasaChai.Application.Listings;
using BasaChai.Application.Moderation;
using BasaChai.Domain;
using BasaChai.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BasaChai.Api.Endpoints;

public sealed record SeedBody(string? Dataset);
public sealed record ClockBody(double OffsetDays);
public sealed record ActBody(string ListingId, string Action, JsonElement? Data);

/// <summary>
/// Hooks for automated tests: reset data, move time, set up state, read back results.
/// Mapped only when the environment is Test or Staging; production builds never expose them.
/// </summary>
public static class TestEndpoints
{
    public static void MapTestHooks(this IEndpointRouteBuilder app)
    {
        var t = app.MapGroup("/v1/test").AddEndpointFilter(async (ctx, next) =>
        {
            var key = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Test:HookKey"];
            if (!string.IsNullOrEmpty(key) && ctx.HttpContext.Request.Headers["X-Test-Key"] != key)
                return Results.NotFound();   // look like the hooks do not exist
            return await next(ctx);
        });

        // Seeding and moving the clock change what every page shows (expiry is decided when read): drop all cached pages.
        t.MapPost("/seed", async (SeedBody body, TestSeeder seeder, IClockControl clockControl, TimeProvider clock, IAppDbContext db, IPageCache pages) =>
        {
            // Last line of defence against a mis-pointed connection string: never wipe a database not meant for tests.
            var name = db.Database.GetDbConnection().Database;
            if (!(name.Contains("test", StringComparison.OrdinalIgnoreCase) || name.Contains("staging", StringComparison.OrdinalIgnoreCase)))
                throw Errors.Conflict("not_a_test_database", "Seeding is only allowed on test or staging databases.");
            clockControl.Reset();
            await seeder.SeedAsync(body.Dataset ?? "pilot", clock.GetUtcNow());
            await pages.InvalidateAsync([], everything: true);
            return Results.NoContent();
        });

        t.MapPost("/clock", async (ClockBody body, IClockControl clockControl, IPageCache pages) =>
        {
            clockControl.Advance(TimeSpan.FromDays(body.OffsetDays));
            await pages.InvalidateAsync([], everything: true);
            return Results.NoContent();
        });

        t.MapPost("/act", async (ActBody body, IAppDbContext db, OwnerListingService owner, ModerationService moderation) =>
        {
            var listing = await db.Listings.AsNoTracking().FirstOrDefaultAsync(l => l.ShortId == body.ListingId) ?? throw new NotFoundException();
            var staffId = TestSeeder.DeterministicId("admin");
            var reason = body.Data?.TryGetProperty("reason", out var r) == true ? r.GetString() : "Reason from the test hook";
            switch (body.Action)
            {
                case "edit":
                    var input = body.Data?.Deserialize<ListingInput>(Json.Web) ?? new ListingInput();
                    await owner.UpdateAsync(listing.OwnerId, listing.ShortId, input);
                    break;
                case "submit": await owner.SubmitAsync(listing.OwnerId, listing.ShortId); break;
                case "approve": await moderation.ApproveAsync(staffId, listing.ShortId); break;
                case "reject": await moderation.DeclineAsync(staffId, listing.ShortId, "reject", reason); break;
                case "request-changes": await moderation.DeclineAsync(staffId, listing.ShortId, "request-changes", reason); break;
                case "hide": await moderation.HideAsync(staffId, listing.ShortId); break;
                case "unhide": await moderation.UnhideAsync(staffId, listing.ShortId); break;
                default: await owner.ActAsync(listing.OwnerId, listing.ShortId, body.Action); break;
            }
            return Results.NoContent();
        });

        t.MapGet("/listings/{shortId}", async (string shortId, IAppDbContext db) =>
        {
            var l = await db.Listings.AsNoTracking().FirstOrDefaultAsync(x => x.ShortId == shortId) ?? throw new NotFoundException();
            return Results.Ok(new
            {
                l.Id, l.ShortId, status = l.Status.ToString().ToLowerInvariant(), l.IsHidden, l.MonthlyRent, l.ServiceCharge,
                l.SubmittedAt, l.PublishedAt, l.LastConfirmedAt, l.ExpiresAt, l.ModeratorReason, l.ViewsCount, l.RevealsCount,
            });
        });

        t.MapGet("/audit", async (string? entity, string? id, IAppDbContext db) =>
        {
            var entityId = id;
            if (entity == "listing" && id is not null && !Guid.TryParse(id, out _))
                entityId = (await db.Listings.Where(l => l.ShortId == id).Select(l => (Guid?)l.Id).FirstOrDefaultAsync())?.ToString();
            var q = db.AuditLog.AsNoTracking();
            if (entity is not null) q = q.Where(a => a.EntityType == entity);
            if (entityId is not null) q = q.Where(a => a.EntityId == entityId);
            var rows = await q.OrderByDescending(a => a.CreatedAt).Take(50).ToListAsync();
            return Results.Ok(rows.Select(a => new { a.Action, a.ActorId, a.CreatedAt, before = a.Before?.RootElement.Clone(), after = a.After?.RootElement.Clone() }));
        });

        t.MapGet("/events", async (string? name, IAppDbContext db) =>
        {
            var q = db.AnalyticsEvents.AsNoTracking();
            if (name is not null) q = q.Where(e => e.Name == name);
            var rows = await q.OrderByDescending(e => e.CreatedAt).Take(100).ToListAsync();
            rows.Reverse();
            return Results.Ok(rows.Select(e => new { e.Name, e.UserId, e.CreatedAt, props = e.Props.RootElement.Clone() }));
        });
    }
}
