using System.Text.Json;
using BasaChai.Application.Common;
using BasaChai.Application.Listings;
using BasaChai.Domain;
using BasaChai.Domain.Listings;
using Microsoft.EntityFrameworkCore;

namespace BasaChai.Application.Moderation;

public sealed record QueueItemDto(Guid Id, string ShortId, string Status, bool IsHidden, CardDto Card, string? Description,
    string OwnerName, Guid OwnerId, DateTimeOffset? SubmittedAt, bool Overdue, IReadOnlyList<OneTimeLineDto> OneTimeCosts);
public sealed record ReportDto(Guid Id, string ListingShortId, Guid ListingId, string ListingTitle, string Reason, string? Details,
    DateTimeOffset CreatedAt, bool Overdue, bool Anonymous);
public sealed record StatsDto(int Pending, int Live, int OpenReports, int Overdue, MetricsDto Metrics);
public sealed record MetricsDto(double? Completeness, double? Freshness, double? SearchToContact, int OwnersActivated, double? ReportHandling);
public sealed record HistoryItemDto(DateTimeOffset At, string Kind, string Action, string? ActorType, Guid? ActorId,
    string? Reason, JsonElement? Before, JsonElement? After);

/// <summary>Review queue, decisions, reports and listing history for moderators and admins.</summary>
public sealed class ModerationService(IAppDbContext db, ReferenceCache refs, TimeProvider clock)
{
    public static readonly TimeSpan OverdueAfter = TimeSpan.FromHours(24);

    public async Task<IReadOnlyList<QueueItemDto>> ListingsAsync(string? status, string? q, string? area, Guid? ownerId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var r = await refs.GetAsync(db, ct);
        var query = db.Listings.Include(l => l.Charges).Include(l => l.Photos).AsNoTracking().AsSplitQuery();
        if (status is not null)
        {
            if (!ListingStatusNames.TryParse(status, out var s)) throw ValidationFailedException.For("status", "invalid");
            query = s == ListingStatus.Expired
                ? query.Where(l => l.Status == ListingStatus.Expired || (l.Status == ListingStatus.Published && l.ExpiresAt <= now))
                : s == ListingStatus.Published
                    ? query.Where(l => l.Status == ListingStatus.Published && l.ExpiresAt > now)
                    : query.Where(l => l.Status == s);
        }
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(l => l.Title.Contains(q.Trim()) || l.ShortId == q.Trim());
        if (r.AreaBySlug(area) is { } a) { var ids = r.SelfAndDescendants(a); query = query.Where(l => l.LocationId != null && ids.Contains(l.LocationId.Value)); }
        if (ownerId is { } o) query = query.Where(l => l.OwnerId == o);

        // The review queue is oldest first; other lists show the most recently changed first.
        query = status == "pending" ? query.OrderBy(l => l.SubmittedAt).ThenBy(l => l.ShortId) : query.OrderByDescending(l => l.UpdatedAt);
        var listings = await query.Take(200).ToListAsync(ct);
        var ownerIds = listings.Select(l => l.OwnerId).Distinct().ToList();
        var names = await db.Users.Where(u => ownerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        return listings.Select(l => new QueueItemDto(
            l.Id, l.ShortId, l.EffectiveStatus(now).ToValue(), l.IsHidden, l.ToCard(r), l.Description,
            names.GetValueOrDefault(l.OwnerId, ""), l.OwnerId, l.SubmittedAt,
            l.Status == ListingStatus.Pending && l.SubmittedAt is { } at && now - at > OverdueAfter,
            l.Costs().OneTime.Select(c => new OneTimeLineDto(c.Label, c.Amount, c.IsAdvance)).ToList())).ToList();
    }

    public async Task ApproveAsync(Guid moderatorId, string key, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var l = await PendingOrHandled(key, ct);
        // A listing must not go live for an owner who has since been suspended or deleted their account.
        if (!await db.Users.AnyAsync(u => u.Id == l.OwnerId && u.Status != "suspended" && u.DeletedAt == null, ct))
            throw Errors.Conflict("owner_unavailable", "The owner's account is suspended or deleted.");
        var hours = l.SubmittedAt is { } at ? Math.Round((now - at).TotalHours, 1) : 0;
        l.Approve(moderatorId, now);
        Record(l, moderatorId, "approve", null, now);
        db.Track(now, "listing_published", new { listingId = l.ShortId, hoursInQueue = hours }, null);
        await SaveOrHandled(ct);
    }

    public async Task DeclineAsync(Guid moderatorId, string key, string action, string? reason, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var l = await PendingOrHandled(key, ct);
        if (action == "reject") l.Reject(moderatorId, reason, now); else l.RequestChanges(moderatorId, reason, now);
        Record(l, moderatorId, action, l.ModeratorReason, now);
        await SaveOrHandled(ct);
    }

    public async Task HideAsync(Guid moderatorId, string key, Guid? reportId = null, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var l = await Find(key, ct);
        if (!l.IsHidden) l.Hide(now);
        // Hiding answers every open report on the listing, not only the one acted on.
        var open = await db.Reports.Where(r => r.ListingId == l.Id && r.Status == "open").ToListAsync(ct);
        foreach (var rep in open)
        {
            rep.Status = "hidden"; rep.ResolvedAt = now; rep.ResolvedBy = moderatorId;
            db.Audit(now, moderatorId, "report.resolve", "report", rep.Id.ToString(), null, new { action = "hide" });
        }
        Record(l, moderatorId, "hide", null, now, reportId);
        await SaveOrHandled(ct);
    }

    public async Task UnhideAsync(Guid moderatorId, string key, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var l = await Find(key, ct);
        l.Unhide();
        Record(l, moderatorId, "unhide", null, now);
        await SaveOrHandled(ct);
    }

    public async Task<IReadOnlyList<ReportDto>> OpenReportsAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var rows = await db.Reports.Where(r => r.Status == "open").OrderBy(r => r.CreatedAt)
            .Select(r => new { r.Id, r.ListingId, r.Listing.ShortId, r.Listing.Title, r.Reason, r.Details, r.CreatedAt, r.ReporterId })
            .ToListAsync(ct);
        return rows.Select(r => new ReportDto(r.Id, r.ShortId, r.ListingId, r.Title, r.Reason, r.Details, r.CreatedAt,
            now - r.CreatedAt > OverdueAfter, r.ReporterId is null)).ToList();
    }

    public async Task ResolveReportAsync(Guid moderatorId, Guid reportId, string action, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var report = await db.Reports.Include(r => r.Listing).FirstOrDefaultAsync(r => r.Id == reportId, ct) ?? throw new NotFoundException();
        if (report.Status != "open") throw Errors.Conflict("already_handled", "This report was already handled.");
        switch (action)
        {
            case "hide":
                await HideAsync(moderatorId, report.Listing.Id.ToString(), report.Id, ct);
                return;
            case "dismiss":
                report.Status = "dismissed"; report.ResolvedAt = now; report.ResolvedBy = moderatorId;
                db.ModerationActions.Add(new ModerationAction { ListingId = report.ListingId, ReportId = report.Id, ModeratorId = moderatorId, Action = "dismiss", CreatedAt = now });
                db.Audit(now, moderatorId, "report.resolve", "report", report.Id.ToString(), null, new { action = "dismiss" });
                await SaveOrHandled(ct);
                return;
            default:
                throw ValidationFailedException.For("action", "invalid");
        }
    }

    public async Task<StatsDto> StatsAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var overdueBefore = now - OverdueAfter;
        var pending = await db.Listings.CountAsync(l => l.Status == ListingStatus.Pending, ct);
        var live = await PublicListingService.Searchable(db, now).CountAsync(ct);
        var openReports = await db.Reports.CountAsync(r => r.Status == "open", ct);
        var overdue = await db.Listings.CountAsync(l => l.Status == ListingStatus.Pending && l.SubmittedAt < overdueBefore, ct)
                      + await db.Reports.CountAsync(r => r.Status == "open" && r.CreatedAt < overdueBefore, ct);
        return new StatsDto(pending, live, openReports, overdue, await MetricsAsync(now, live, ct));
    }

    /// <summary>The five pilot metrics over the last 30 days, computed on demand (no rollups, D12).</summary>
    private async Task<MetricsDto> MetricsAsync(DateTimeOffset now, int live, CancellationToken ct)
    {
        var since = now.AddDays(-30);
        var liveListings = PublicListingService.Searchable(db, now);
        var complete = await liveListings.CountAsync(l => l.Photos.Count >= Listing.MinPhotos && l.Bathrooms != null && l.MonthlyRent > 0
                                                          && l.ServiceCharge != null && l.Charges.Any(c => c.Kind == ChargeKind.Advance), ct);
        var fresh = await liveListings.CountAsync(l => l.LastConfirmedAt > since, ct);
        var searchSessions = await db.AnalyticsEvents.Where(e => e.Name == "search_performed" && e.CreatedAt > since && e.SessionId != null)
            .Select(e => e.SessionId).Distinct().CountAsync(ct);
        var contactSessions = await db.AnalyticsEvents.Where(e => e.Name == "contact_revealed" && e.CreatedAt > since && e.SessionId != null)
            .Select(e => e.SessionId).Distinct().CountAsync(ct);
        var activated = await db.Listings.Where(l => l.PublishedAt > since).Select(l => l.OwnerId).Distinct().CountAsync(ct);
        var reports = await db.Reports.Where(r => r.CreatedAt > since).Select(r => new { r.CreatedAt, r.ResolvedAt }).ToListAsync(ct);
        var answered = reports.Count(r => r.ResolvedAt is { } at ? at - r.CreatedAt <= OverdueAfter : now - r.CreatedAt <= OverdueAfter);
        static double? Share(int part, int whole) => whole == 0 ? null : Math.Round((double)part / whole, 3);
        return new MetricsDto(Share(complete, live), Share(fresh, live), Share(contactSessions, searchSessions), activated, Share(answered, reports.Count));
    }

    /// <summary>Status changes, moderator actions and Live edits, oldest first. This is the check on D23.</summary>
    public async Task<IReadOnlyList<HistoryItemDto>> HistoryAsync(string key, CancellationToken ct = default)
    {
        var l = await Find(key, ct);
        var id = l.Id.ToString();
        var status = await db.AvailabilityHistory.Where(h => h.ListingId == l.Id).AsNoTracking().ToListAsync(ct);
        var actions = await db.ModerationActions.Where(m => m.ListingId == l.Id).AsNoTracking().ToListAsync(ct);
        var edits = await db.AuditLog.Where(a => a.EntityType == "listing" && a.EntityId == id && (a.Action == "listing.edit" || a.Action == "listing.photos")).AsNoTracking().ToListAsync(ct);
        return status.Select(h => new HistoryItemDto(h.CreatedAt, "status", $"{h.FromStatus}->{h.ToStatus}", h.ActorType, h.ActorId, null, null, null))
            .Concat(actions.Select(m => new HistoryItemDto(m.CreatedAt, "moderation", m.Action, "moderator", m.ModeratorId, m.Reason, null, null)))
            .Concat(edits.Select(a => new HistoryItemDto(a.CreatedAt, "edit", a.Action, "owner", a.ActorId, null,
                a.Before?.RootElement.Clone(), a.After?.RootElement.Clone())))
            .OrderBy(h => h.At).ToList();
    }

    private void Record(Listing l, Guid moderatorId, string action, string? reason, DateTimeOffset now, Guid? reportId = null)
    {
        db.FlushHistory(l);
        l.UpdatedAt = now;
        db.ModerationActions.Add(new ModerationAction { ListingId = l.Id, ReportId = reportId, ModeratorId = moderatorId, Action = action, Reason = reason, CreatedAt = now });
        db.Audit(now, moderatorId, "listing." + action, "listing", l.Id.ToString(), null, reason is null ? new { status = l.Status.ToValue() } : new { status = l.Status.ToValue(), reason });
    }

    private async Task<Listing> PendingOrHandled(string key, CancellationToken ct)
    {
        var l = await Find(key, ct);
        // A second moderator acting on the same item gets a clear answer instead of a generic transition error.
        if (l.Status != ListingStatus.Pending) throw Errors.Conflict("already_handled", "This listing was already handled.");
        return l;
    }

    private async Task SaveOrHandled(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw Errors.Conflict("already_handled", "This listing was already handled."); }
    }

    private async Task<Listing> Find(string key, CancellationToken ct)
    {
        var byId = Guid.TryParse(key, out var id);
        return await db.Listings.Include(l => l.Charges).FirstOrDefaultAsync(l => byId ? l.Id == id : l.ShortId == key, ct)
               ?? throw new NotFoundException();
    }
}
