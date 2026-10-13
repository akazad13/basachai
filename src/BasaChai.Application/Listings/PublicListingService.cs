using BasaChai.Application.Common;
using BasaChai.Domain;
using BasaChai.Domain.Listings;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace BasaChai.Application.Listings;

/// <summary>Everything a visitor can do without signing in: search, view, reveal a number, report.</summary>
public sealed class PublicListingService(IAppDbContext db, ReferenceCache refs, TimeProvider clock, IRateLimiter limiter)
{
    public const int RevealsPerHour = 10;
    public const int RevealsPerDay = 30;
    public const int ReportsPerDay = 10;
    public const int EventsPerHour = 300;

    /// <summary>Live, unhidden rentals within 30 days of confirmation. Every public query starts here.</summary>
    public static IQueryable<Listing> Searchable(IAppDbContext db, DateTimeOffset now)
        => db.Listings.Where(l => l.Status == ListingStatus.Published && !l.IsHidden && l.Purpose == "rent" && l.ExpiresAt > now);

    public async Task<SearchPage> SearchAsync(SearchQuery q, string? source, RequestContext? ctx, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var r = await refs.GetAsync(db, ct);
        var query = Filter(Searchable(db, now), q, r, now);

        var count = await query.CountAsync(ct);
        var page = await Page(query, q).Take(q.Size + 1)
            .Include(l => l.Charges).Include(l => l.Photos)
            .AsNoTracking().AsSplitQuery().ToListAsync(ct);

        string? next = null;
        if (page.Count > q.Size)
        {
            page.RemoveAt(page.Count - 1);
            var last = page[^1];
            next = new SearchCursor(q.Sort, SortValue(last, q.Sort), last.ShortId).Encode();
        }

        // One event per search a person makes: "Show more" pages and the home page's recent homes (source "none") are not searches.
        if (q.Cursor is null && source != "none")
        {
            db.Track(now, "search_performed", new
            {
                filters = q.Filters(), sort = q.Sort, resultCount = count,
                source = source is "home" or "search" ? source : null,
            }, ctx);
            await db.SaveChangesAsync(ct);
        }

        return new SearchPage(page.Select(l => l.ToCard(r)).ToList(), count, next);
    }

    private static IQueryable<Listing> Filter(IQueryable<Listing> query, SearchQuery q, ReferenceData r, DateTimeOffset now)
    {
        if (q.Area is not null)
        {
            // An unknown area matches nothing rather than everything: the user asked for a place we do not cover.
            var ids = r.AreaBySlug(q.Area) is { } area ? r.SelfAndDescendants(area) : [];
            query = query.Where(l => l.LocationId != null && ids.Contains(l.LocationId.Value));
        }
        if (q.Min is { } min) query = query.Where(l => l.MonthlyRent >= min);
        if (q.Max is { } max) query = query.Where(l => l.MonthlyRent <= max);
        if (q.Beds is { } beds) query = query.Where(l => l.Bedrooms == beds);
        if (q.Baths is { } baths) query = query.Where(l => l.Bathrooms == baths);
        if (q.Type is not null)
        {
            var typeId = r.TypeByKey(q.Type)?.Id ?? -1;
            query = query.Where(l => l.PropertyTypeId == typeId);
        }
        if (q.Furnish is { } f) query = query.Where(l => l.Furnishing == f);
        if (q.Occ is { } occ) query = query.Where(l => l.Occupancy == occ || l.Occupancy == "any");
        if (q.Avail is { } avail)
        {
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Dhaka).DateTime);
            var by = avail == "now" ? today : today.AddDays(30);
            query = query.Where(l => l.AvailableFrom == null || l.AvailableFrom <= by);
        }
        if (q.Q is { } text)
            query = query.Where(l => EF.Property<NpgsqlTsVector>(l, "SearchVector").Matches(EF.Functions.PlainToTsQuery("simple", text)));
        return query;
    }

    private static IQueryable<Listing> Page(IQueryable<Listing> query, SearchQuery q)
    {
        var c = SearchCursor.Decode(q.Cursor, q.Sort);
        switch (q.Sort)
        {
            case "rent_asc":
                if (c is not null) query = query.Where(l => l.MonthlyRent > c.V || (l.MonthlyRent == c.V && string.Compare(l.ShortId, c.K) > 0));
                return query.OrderBy(l => l.MonthlyRent).ThenBy(l => l.ShortId);
            case "rent_desc":
                if (c is not null) query = query.Where(l => l.MonthlyRent < c.V || (l.MonthlyRent == c.V && string.Compare(l.ShortId, c.K) < 0));
                return query.OrderByDescending(l => l.MonthlyRent).ThenByDescending(l => l.ShortId);
            case "newest":
                if (c is not null)
                {
                    var at = FromTicks(c.V);
                    query = query.Where(l => l.PublishedAt < at || (l.PublishedAt == at && string.Compare(l.ShortId, c.K) < 0));
                }
                return query.OrderByDescending(l => l.PublishedAt).ThenByDescending(l => l.ShortId);
            default:
                if (c is not null)
                {
                    var at = FromTicks(c.V);
                    query = query.Where(l => l.LastConfirmedAt < at || (l.LastConfirmedAt == at && string.Compare(l.ShortId, c.K) < 0));
                }
                return query.OrderByDescending(l => l.LastConfirmedAt).ThenByDescending(l => l.ShortId);
        }
    }

    private static long SortValue(Listing l, string sort) => sort switch
    {
        "rent_asc" or "rent_desc" => l.MonthlyRent ?? 0,
        "newest" => l.PublishedAt!.Value.UtcTicks,
        _ => l.LastConfirmedAt!.Value.UtcTicks,
    };

    // PostgreSQL stores microseconds, so ticks round-trip exactly only after truncating to them.
    private static DateTimeOffset FromTicks(long ticks) => new(ticks - ticks % 10, TimeSpan.Zero);

    public static readonly TimeZoneInfo Dhaka = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");

    public async Task<ListingDetailDto?> DetailAsync(string shortId, RequestContext? ctx, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var l = await db.Listings.Include(x => x.Charges).Include(x => x.Photos).Include(x => x.Amenities)
            .AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(x => x.ShortId == shortId, ct);
        if (l is null) return null;
        var effective = l.EffectiveStatus(now);
        var canPreview = ctx is not null && (ctx.UserId == l.OwnerId || ctx.IsStaff);
        if (!l.IsPublic(now) && !canPreview)
            // A listing that was never public (draft, in review, rejected before going live) does not exist for visitors.
            return l.PublishedAt is null ? null : ListingMapping.Unavailable(effective);

        var r = await refs.GetAsync(db, ct);
        var ownerName = await db.Users.Where(u => u.Id == l.OwnerId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) ?? "";
        return l.ToDetail(r, ownerName, effective);
    }

    /// <summary>Reveals the owner's number. Limited per IP and per device: 10 an hour and 30 a day.</summary>
    public async Task<ContactDto> RevealAsync(string shortId, RequestContext ctx, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var listing = await Searchable(db, now).Where(l => l.ShortId == shortId)
                          .Select(l => new { l.Id, l.OwnerId, l.ShortId }).FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException();

        foreach (var key in new[] { "ip:" + ctx.IpKey, "device:" + ctx.DeviceKey })
        {
            var wait = limiter.TryHit("reveal-hour", key, RevealsPerHour, TimeSpan.FromHours(1))
                       ?? limiter.TryHit("reveal-day", key, RevealsPerDay, TimeSpan.FromDays(1));
            if (wait is { } w) throw new RateLimitedException(w);
        }

        var phone = await db.Users.Where(u => u.Id == listing.OwnerId).Select(u => u.PhoneE164).FirstAsync(ct);
        // Atomic increment: concurrent reveals never conflict and never touch the listing's version.
        await db.Listings.Where(l => l.Id == listing.Id).ExecuteUpdateAsync(s => s.SetProperty(l => l.RevealsCount, l => l.RevealsCount + 1), ct);
        db.ContactEvents.Add(new ContactEvent
        {
            ListingId = listing.Id, UserId = ctx.UserId, DeviceHash = ctx.DeviceHash, IpHash = ctx.IpHash, CreatedAt = now,
        });
        db.Track(now, "contact_revealed", new { listingId = listing.ShortId, signedIn = ctx.UserId is not null }, ctx);
        await db.SaveChangesAsync(ct);
        return new ContactDto(phone);
    }

    public async Task ReportAsync(string shortId, string? reason, string? details, RequestContext ctx, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        if (reason is null || !ReportReasons.All.Contains(reason)) throw ValidationFailedException.For("reason", "pick_reason");
        details = string.IsNullOrWhiteSpace(details) ? null : details.Trim();
        if (details is { Length: > 500 }) throw ValidationFailedException.For("details", "too_long");

        // Only listings that have been public can be reported; others do not exist for visitors.
        var listing = await db.Listings.Where(l => l.ShortId == shortId && l.PublishedAt != null)
                          .Select(l => new { l.Id, l.ShortId }).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        if (limiter.TryHit("report-day", "ip:" + ctx.IpKey, ReportsPerDay, TimeSpan.FromDays(1)) is { } wait)
            throw new RateLimitedException(wait);

        db.Reports.Add(new ListingReport
        {
            ListingId = listing.Id, ReporterId = ctx.UserId, ReporterIpHash = ctx.IpHash, Reason = reason, Details = details, CreatedAt = now,
        });
        db.Track(now, "report_created", new { listingId = listing.ShortId, reason, anonymous = ctx.UserId is null }, ctx);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Records a search the browser showed (the page itself may come from the edge cache). The filters are parsed
    /// from the query string exactly as a search is, so only known filters and values are stored.
    /// </summary>
    public async Task RecordSearchAsync(string query, int resultCount, string? source, RequestContext ctx, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        // Its own budget, so searches and views (many visitors can share one mobile IP) do not use up each other's.
        if (limiter.TryHit("search-events-ip", ctx.IpKey, EventsPerHour, TimeSpan.FromHours(1)) is { } wait) throw new RateLimitedException(wait);
        var values = (query.Length > 1000 ? "" : query).TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .GroupBy(kv => Uri.UnescapeDataString(kv[0].Replace('+', ' ')))
            .ToDictionary(g => g.Key, g => g.First() is { Length: 2 } kv ? Uri.UnescapeDataString(kv[1].Replace('+', ' ')) : "");
        var q = SearchQuery.Parse(k => values.GetValueOrDefault(k));
        db.Track(now, "search_performed", new
        {
            filters = q.Filters(), sort = q.Sort, resultCount = Math.Min(resultCount, 100_000),
            source = source is "home" or "search" ? source : null,
        }, ctx);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Counts a view. Repeated views of one listing in one session count once an hour.</summary>
    public async Task RecordViewAsync(string shortId, string? referrer, RequestContext ctx, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        if (!await Searchable(db, now).AnyAsync(l => l.ShortId == shortId, ct)) return;
        if (limiter.TryHit("events-ip", ctx.IpKey, EventsPerHour, TimeSpan.FromHours(1)) is { } wait) throw new RateLimitedException(wait);
        var sessionKey = ctx.SessionKey ?? ctx.DeviceKey;
        if (limiter.TryHit("view", $"{sessionKey}:{shortId}", 1, TimeSpan.FromHours(1)) is not null) return;
        await db.Listings.Where(l => l.ShortId == shortId).ExecuteUpdateAsync(s => s.SetProperty(l => l.ViewsCount, l => l.ViewsCount + 1), ct);
        db.Track(now, "listing_viewed", new
        {
            listingId = shortId,
            referrer = referrer is "search" or "home" or "saved" ? referrer : "direct",
        }, ctx);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The listing used as the home page cost example: the admin's choice, else the freshest with an extra monthly bill.</summary>
    public async Task<CostExampleDto?> CostExampleAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var r = await refs.GetAsync(db, ct);
        var chosen = await db.Settings.Where(s => s.Key == AppSetting.HomeCostExample).Select(s => s.Value).FirstOrDefaultAsync(ct);
        var query = Searchable(db, now).Include(l => l.Charges).Include(l => l.Photos).AsNoTracking().AsSplitQuery();
        var listing = chosen is null ? null : await query.FirstOrDefaultAsync(l => l.ShortId == chosen, ct);
        listing ??= await query.Where(l => l.Charges.Any(c => c.Kind == ChargeKind.OtherMonthly))
            .OrderByDescending(l => l.LastConfirmedAt).ThenByDescending(l => l.ShortId).FirstOrDefaultAsync(ct);
        return listing is null ? null : new CostExampleDto(listing.ToCard(r),
            listing.Costs().OneTime.Select(c => new OneTimeLineDto(c.Label, c.Amount, c.IsAdvance)).ToList());
    }
}
