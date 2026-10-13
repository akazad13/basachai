using BasaChai.Application.Common;
using BasaChai.Domain;
using Microsoft.EntityFrameworkCore;

namespace BasaChai.Application.Listings;

/// <summary>Saved homes. Only homes that are still Live are returned; saving is idempotent.</summary>
public sealed class SavedService(IAppDbContext db, ReferenceCache refs, TimeProvider clock)
{
    public async Task<IReadOnlyList<CardDto>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var r = await refs.GetAsync(db, ct);
        var ids = db.Saved.Where(s => s.UserId == userId).Select(s => s.ListingId);
        var listings = await PublicListingService.Searchable(db, now).Where(l => ids.Contains(l.Id))
            .Include(l => l.Charges).Include(l => l.Photos).AsNoTracking().AsSplitQuery().ToListAsync(ct);
        var order = await db.Saved.Where(s => s.UserId == userId).ToDictionaryAsync(s => s.ListingId, s => s.CreatedAt, ct);
        return listings.OrderByDescending(l => order[l.Id]).Select(l => l.ToCard(r)).ToList();
    }

    public async Task<IReadOnlyList<string>> SavedIdsAsync(Guid userId, CancellationToken ct = default)
        => await db.Saved.Where(s => s.UserId == userId).Select(s => s.Listing.ShortId).ToListAsync(ct);

    public async Task SaveAsync(Guid userId, string key, CancellationToken ct = default)
    {
        var byId = Guid.TryParse(key, out var id);
        // Only a live home can be saved; anything else is "not found" so drafts are not discoverable.
        var listingId = await PublicListingService.Searchable(db, clock.GetUtcNow()).Where(l => byId ? l.Id == id : l.ShortId == key)
                            .Select(l => (Guid?)l.Id).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        var now = clock.GetUtcNow();
        // ON CONFLICT keeps PUT idempotent even when two taps arrive at once.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO saved_listings (user_id, listing_id, created_at) VALUES ({userId}, {listingId}, {now}) ON CONFLICT DO NOTHING", ct);
    }

    public async Task UnsaveAsync(Guid userId, string key, CancellationToken ct = default)
    {
        var listingId = await ResolveAsync(key, ct);
        await db.Saved.Where(s => s.UserId == userId && s.ListingId == listingId).ExecuteDeleteAsync(ct);
    }

    private async Task<Guid> ResolveAsync(string key, CancellationToken ct)
    {
        var byId = Guid.TryParse(key, out var id);
        return await db.Listings.Where(l => byId ? l.Id == id : l.ShortId == key).Select(l => (Guid?)l.Id).FirstOrDefaultAsync(ct)
               ?? throw new NotFoundException();
    }
}
