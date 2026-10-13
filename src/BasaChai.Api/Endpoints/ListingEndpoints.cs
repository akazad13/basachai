using BasaChai.Api.Http;
using BasaChai.Application.Common;
using BasaChai.Application.Identity;
using BasaChai.Application.Listings;
using BasaChai.Domain;
using BasaChai.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasaChai.Api.Endpoints;

public sealed record ReportBody(string? Reason, string? Details);
public sealed record EventBody(string? Name, string? ListingId, string? Referrer, string? Query = null, int? ResultCount = null, string? Source = null);
public sealed record PhotoOrderBody(IReadOnlyList<Guid>? Order);

public static class ListingEndpoints
{
    public static void MapListings(this IEndpointRouteBuilder app)
    {
        var pub = app.MapGroup("/v1");

        pub.MapGet("/listings", async (HttpContext http, PublicListingService svc) =>
        {
            http.Response.Headers.CacheControl = "public, s-maxage=60";
            var q = SearchQuery.Parse(k => http.Request.Query[k].FirstOrDefault());
            var source = http.Request.Headers["X-Search-Source"].FirstOrDefault();
            return Results.Ok(await svc.SearchAsync(q, source, http.Context()));
        });

        pub.MapGet("/listings/{shortId}", async (string shortId, HttpContext http, PublicListingService svc) =>
        {
            var detail = await svc.DetailAsync(shortId, http.User.Identity?.IsAuthenticated == true ? http.Context() : null);
            if (detail is null) throw new NotFoundException();
            // Owner and staff previews of non-live listings must not be cached at the edge.
            http.Response.Headers.CacheControl = detail.Available ? "public, s-maxage=60" : "private, no-store";
            return Results.Ok(detail);
        });

        pub.MapPost("/listings/{shortId}/contact", async (string shortId, HttpContext http, PublicListingService svc)
            => Results.Ok(await svc.RevealAsync(shortId, http.Context(issueCookies: true))));

        pub.MapPost("/listings/{shortId}/reports", async (string shortId, ReportBody body, HttpContext http, PublicListingService svc) =>
        {
            await svc.ReportAsync(shortId, body.Reason, body.Details, http.Context(issueCookies: true));
            return Results.Created();
        });

        pub.MapPost("/events", async (EventBody body, HttpContext http, PublicListingService svc) =>
        {
            // Only client-side events are accepted here; everything else is recorded by the API itself.
            // Searches are reported by the browser because search and area pages may be served from the edge cache.
            switch (body.Name)
            {
                case "listing_viewed" when !string.IsNullOrEmpty(body.ListingId):
                    await svc.RecordViewAsync(body.ListingId, body.Referrer, http.Context(issueCookies: true));
                    break;
                case "search_performed" when body.ResultCount is >= 0:
                    await svc.RecordSearchAsync(body.Query ?? "", body.ResultCount.Value, body.Source, http.Context(issueCookies: true));
                    break;
                default: throw ValidationFailedException.For("name", "invalid");
            }
            return Results.Accepted();
        });

        pub.MapGet("/home", async (HttpContext http, PublicListingService svc) =>
        {
            http.Response.Headers.CacheControl = "public, s-maxage=60";
            return Results.Ok(new { costExample = await svc.CostExampleAsync() });
        });

        pub.MapGet("/locations", async (HttpContext http, IAppDbContext db, ReferenceCache refs) =>
        {
            http.Response.Headers.CacheControl = "public, s-maxage=300";
            var r = await refs.GetAsync(db);
            var upazila = r.Locations.First(l => l.Kind == "upazila");
            return Results.Ok(new
            {
                slug = upazila.Slug, nameBn = upazila.NameBn, nameEn = upazila.NameEn,
                areas = r.ActiveAreas.Select(a => new AreaDto(a.Slug, a.NameBn, a.NameEn)),
            });
        });

        pub.MapGet("/locations/suggest", async (string? q, IAppDbContext db, ReferenceCache refs) =>
        {
            var norm = ReferenceSeeder.Normalize(q ?? "");
            if (norm.Length < 2) return Results.Ok(Array.Empty<AreaDto>());
            var r = await refs.GetAsync(db);
            // Trigram similarity tolerates spelling variants such as Dakkhin / Dokkhin.
            var ids = await db.LocationAliases
                .Where(a => a.Normalized.Contains(norm) || EF.Functions.TrigramsSimilarity(a.Normalized, norm) > 0.3)
                .OrderByDescending(a => EF.Functions.TrigramsSimilarity(a.Normalized, norm))
                .Select(a => a.LocationId).Take(20).ToListAsync();
            return Results.Ok(ids.Distinct().Select(id => r.LocationById.GetValueOrDefault(id)).Where(l => l is { IsActive: true })
                .Take(5).Select(l => new AreaDto(l!.Slug, l.NameBn, l.NameEn)));
        });

        // Development and Test only: serves photos kept by LocalPhotoStore when Cloudinary is not configured.
        pub.MapGet("/dev-photos/{name}", (string name, IServiceProvider sp) =>
        {
            var store = sp.GetService<BasaChai.Infrastructure.Services.LocalPhotoStore>();
            var path = store?.TryResolve(BasaChai.Infrastructure.Services.LocalPhotoStore.Prefix + name);
            return path is not null && File.Exists(path) ? Results.File(path, "image/jpeg") : Results.NotFound();
        });

        pub.MapGet("/property-types", async (IAppDbContext db, ReferenceCache refs)
            => Results.Ok((await refs.GetAsync(db)).Types.Where(t => t.IsActive).Select(t => new NamedDto(t.Key, t.NameBn, t.NameEn))));

        pub.MapGet("/amenities", async (IAppDbContext db, ReferenceCache refs)
            => Results.Ok((await refs.GetAsync(db)).Amenities.Where(a => a.IsActive).Select(a => new NamedDto(a.Key, a.NameBn, a.NameEn))));

        // ---------- Owner ----------

        var owner = app.MapGroup("/v1/owner/listings").RequireAuthorization();

        owner.MapGet("", async (HttpContext http, OwnerListingService svc) => Results.Ok(await svc.ListAsync(http.User.RequiredUserId())));

        owner.MapPost("", async (ListingInput body, HttpContext http, OwnerListingService svc, UserManager<AppUser> users, SignInManager<AppUser> signIn) =>
        {
            var user = await users.GetUserAsync(http.User) ?? throw new DomainException("unauthenticated", "Sign in first.", 401);
            if (user.Status != UserStatus.Active) throw new ForbiddenException("restricted", "This account cannot post listings. Contact support.");
            // Posting a property makes a member an owner (owners only at launch, D2).
            if (!await users.IsInRoleAsync(user, Roles.Owner))
            {
                await users.AddToRoleAsync(user, Roles.Owner);
                await signIn.RefreshSignInAsync(user);
            }
            var created = await svc.CreateAsync(user.Id, body);
            return Results.Created($"/v1/owner/listings/{created.Id}", created);
        });

        owner.MapGet("/{key}", async (string key, HttpContext http, OwnerListingService svc) => Results.Ok(await svc.GetAsync(http.User.RequiredUserId(), key)));

        owner.MapPatch("/{key}", async (string key, ListingInput body, HttpContext http, OwnerListingService svc, UserManager<AppUser> users) =>
        {
            await RequireActive(http, users);
            int? expected = int.TryParse(http.Request.Headers.IfMatch.FirstOrDefault()?.Trim('"', 'W', '/'), out var v) ? v : null;
            return Results.Ok(await svc.UpdateAsync(http.User.RequiredUserId(), key, body, expected));
        });

        owner.MapPost("/{key}/submit", async (string key, HttpContext http, OwnerListingService svc, UserManager<AppUser> users) =>
        {
            await RequireActive(http, users);
            return Results.Ok(await svc.SubmitAsync(http.User.RequiredUserId(), key));
        });

        // A restricted owner may take a listing down (pause, mark rented, archive) but not put anything back up.
        foreach (var action in new[] { "reconfirm", "renew", "pause", "resume", "mark-rented", "archive" })
            owner.MapPost($"/{{key}}/{action}", async (string key, HttpContext http, OwnerListingService svc, UserManager<AppUser> users) =>
            {
                if (action is "reconfirm" or "renew" or "resume") await RequireActive(http, users);
                return Results.Ok(await svc.ActAsync(http.User.RequiredUserId(), key, action));
            });

        owner.MapPost("/{key}/photos", async (string key, IFormFile? file, HttpContext http, OwnerListingService svc, UserManager<AppUser> users) =>
        {
            await RequireActive(http, users);
            if (file is null) throw ValidationFailedException.For("file", "required");
            await using var stream = new MemoryStream();
            if (file.Length <= OwnerListingService.MaxPhotoBytes) await file.CopyToAsync(stream);
            stream.Position = 0;
            return Results.Created("", await svc.AddPhotoAsync(http.User.RequiredUserId(), key, stream, file.Length));
        }).DisableAntiforgery();

        owner.MapPatch("/{key}/photos", async (string key, PhotoOrderBody body, HttpContext http, OwnerListingService svc, UserManager<AppUser> users) =>
        {
            await RequireActive(http, users);
            return Results.Ok(await svc.ReorderPhotosAsync(http.User.RequiredUserId(), key, body.Order ?? []));
        });

        owner.MapDelete("/{key}/photos/{photoId:guid}", async (string key, Guid photoId, HttpContext http, OwnerListingService svc, UserManager<AppUser> users) =>
        {
            await RequireActive(http, users);
            await svc.DeletePhotoAsync(http.User.RequiredUserId(), key, photoId);
            return Results.NoContent();
        });
    }

    private static async Task RequireActive(HttpContext http, UserManager<AppUser> users)
    {
        var user = await users.GetUserAsync(http.User) ?? throw new DomainException("unauthenticated", "Sign in first.", 401);
        if (user.Status != UserStatus.Active) throw new ForbiddenException("restricted", "This account cannot post listings. Contact support.");
    }
}
