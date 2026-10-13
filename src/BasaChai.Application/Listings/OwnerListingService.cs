using BasaChai.Application.Common;
using BasaChai.Domain;
using BasaChai.Domain.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BasaChai.Application.Listings;

public sealed record PhotoOptions(string Folder);

/// <summary>An owner's own listings: drafts, edits, lifecycle actions and photos.</summary>
public sealed class OwnerListingService(
    IAppDbContext db, ReferenceCache refs, TimeProvider clock, IRateLimiter limiter,
    IAddressProtector addresses, IPhotoStore photos, PhotoOptions photoOptions, ILogger<OwnerListingService> log)
{
    public const int SubmissionsPerDay = 10;
    public const int UploadsPerHour = 60;
    public const long MaxPhotoBytes = 10 * 1024 * 1024;

    public async Task<IReadOnlyList<OwnerListingDto>> ListAsync(Guid ownerId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var listings = await Load().Where(l => l.OwnerId == ownerId && l.Status != ListingStatus.Archived).ToListAsync(ct);
        // Visiting the dashboard stores any read-time expiry (no background jobs, D12).
        foreach (var l in listings.Where(l => l.ExpireIfDue(now))) db.FlushHistory(l);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            // Another request (a second tab, a moderator) changed one of these listings at the same moment.
            // Effective statuses are still right; the expiry is stored on the next visit.
        }
        var r = await refs.GetAsync(db, ct);
        return listings.OrderBy(l => DashboardOrder(l.EffectiveStatus(now))).ThenByDescending(l => l.UpdatedAt)
            .Select(l => l.ToOwnerDto(r, now, null)).ToList();
    }

    /// <summary>Changes needed, Draft, Expired, In review, Live, Paused, Rented, Rejected (owner-dashboard spec).</summary>
    public static int DashboardOrder(ListingStatus s) => s switch
    {
        ListingStatus.Changes => 0, ListingStatus.Draft => 1, ListingStatus.Expired => 2, ListingStatus.Pending => 3,
        ListingStatus.Published => 4, ListingStatus.Paused => 5, ListingStatus.Rented => 6, ListingStatus.Rejected => 7, _ => 8,
    };

    public async Task<OwnerListingDto> GetAsync(Guid ownerId, string key, CancellationToken ct = default)
    {
        var l = await Find(ownerId, key, ct);
        var r = await refs.GetAsync(db, ct);
        return l.ToOwnerDto(r, clock.GetUtcNow(), l.FullAddressEnc is null ? null : addresses.Unprotect(l.FullAddressEnc));
    }

    public async Task<OwnerListingDto> CreateAsync(Guid ownerId, ListingInput input, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var r = await refs.GetAsync(db, ct);
        var listing = new Listing { OwnerId = ownerId, ShortId = await NewShortId(ct), CreatedAt = now, UpdatedAt = now };
        ListingEditor.Apply(listing, input, r, addresses);
        if (string.IsNullOrWhiteSpace(listing.Title)) throw ValidationFailedException.For("title", "required");
        db.Listings.Add(listing);
        await db.SaveChangesAsync(ct);
        return listing.ToOwnerDto(r, now, input.FullAddress);
    }

    /// <summary>Edits a draft, a listing that needs changes, or a Live one. Live edits stay Live and are audited (D23).</summary>
    public async Task<OwnerListingDto> UpdateAsync(Guid ownerId, string key, ListingInput input, int? expectedVersion = null, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var r = await refs.GetAsync(db, ct);
        var l = await Find(ownerId, key, ct);
        // If-Match: the client says which version it edited; a different stored version means another tab saved first.
        if (expectedVersion is { } v && v != l.Version)
            throw Errors.Conflict("stale_listing", "This listing changed since you opened it. Reload and try again.");
        if (l.ExpireIfDue(now)) db.FlushHistory(l);
        if (!l.CanEdit(now)) throw new InvalidTransitionException(l.Status.ToValue(), "edit");

        var before = ListingEditor.Snapshot(l, r, addresses);
        ListingEditor.Apply(l, input, r, addresses);
        if (l.Status == ListingStatus.Published)
        {
            // A Live listing must stay complete: the same rules as posting apply.
            var missing = l.MissingForSubmit();
            if (missing.Count > 0) throw new ValidationFailedException(missing);
            var (b, a) = ListingEditor.Diff(before, ListingEditor.Snapshot(l, r, addresses));
            if (b.Count > 0) db.Audit(now, ownerId, "listing.edit", "listing", l.Id.ToString(), b, a);
        }
        l.UpdatedAt = now;
        await Save(ct);
        return l.ToOwnerDto(r, now, l.FullAddressEnc is null ? null : addresses.Unprotect(l.FullAddressEnc));
    }

    public async Task<OwnerListingDto> SubmitAsync(Guid ownerId, string key, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var l = await Find(ownerId, key, ct);
        var first = l.Status == ListingStatus.Draft;
        l.Submit(now, Actor.Owner(ownerId));
        if (limiter.TryHit("submit-day", ownerId.ToString(), SubmissionsPerDay, TimeSpan.FromDays(1)) is { } wait)
            throw new RateLimitedException(wait);
        l.UpdatedAt = now;
        db.FlushHistory(l);
        db.Track(now, "listing_submitted", new { listingId = l.ShortId, first }, null);
        await Save(ct);
        return l.ToOwnerDto(await refs.GetAsync(db, ct), now, null);
    }

    /// <summary>reconfirm, pause, resume, mark-rented, renew or archive.</summary>
    public async Task<OwnerListingDto> ActAsync(Guid ownerId, string key, string action, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var l = await Find(ownerId, key, ct);
        var actor = Actor.Owner(ownerId);
        var confirmedBefore = l.LastConfirmedAt;
        var wasExpired = l.EffectiveStatus(now) == ListingStatus.Expired;
        switch (action)
        {
            case "reconfirm": l.Reconfirm(now, actor); break;
            case "renew": l.Renew(now, actor); break;
            case "pause": l.Pause(now, actor); break;
            case "resume": l.Resume(now, actor); break;
            case "mark-rented": l.MarkRented(now, actor); break;
            case "archive": l.Archive(now, actor); break;
            default: throw new NotFoundException();
        }
        if (action is "reconfirm" or "renew")
            db.Track(now, "listing_reconfirmed", new
            {
                listingId = l.ShortId, wasExpired,
                daysSinceConfirm = confirmedBefore is { } c ? (int)(now - c).TotalDays : (int?)null,
            }, null);
        l.UpdatedAt = now;
        db.FlushHistory(l);
        await Save(ct);
        return l.ToOwnerDto(await refs.GetAsync(db, ct), now, null);
    }

    /// <summary>Listings that need the owner: changes requested, expired, or Live and expiring within 3 days.</summary>
    public async Task<int> AttentionCountAsync(Guid ownerId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var soon = now.AddDays(3);
        return await db.Listings.CountAsync(l => l.OwnerId == ownerId &&
            (l.Status == ListingStatus.Changes || l.Status == ListingStatus.Expired ||
             (l.Status == ListingStatus.Published && l.ExpiresAt <= soon)), ct);
    }

    // ---------- Photos ----------

    public async Task<PhotoDto> AddPhotoAsync(Guid ownerId, string key, Stream content, long length, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var l = await Find(ownerId, key, ct);
        if (!l.CanEdit(now)) throw new InvalidTransitionException(l.Status.ToValue(), "photos");
        if (length > MaxPhotoBytes) throw new DomainException("photo_too_large", "Photos must be 10 MB or smaller.", 413);
        if (!await PhotoSignature.IsImageAsync(content, ct)) throw new DomainException("photo_type", "Use a JPEG, PNG or WebP photo.", 415);
        if (l.Photos.Count >= Listing.MaxPhotos) throw Errors.Conflict("photo_limit", "A listing can have at most 20 photos.");
        if (limiter.TryHit("upload-hour", ownerId.ToString(), UploadsPerHour, TimeSpan.FromHours(1)) is { } wait)
            throw new RateLimitedException(wait);

        StoredPhoto stored;
        try { stored = await photos.UploadAsync(content, $"{photoOptions.Folder}/listings/{l.Id}", ct); }
        catch (PhotoRejectedException) { throw new DomainException("photo_type", "Use a JPEG, PNG or WebP photo.", 415); }
        catch (PhotoStoreException e)
        {
            log.LogError(e, "Photo upload failed for listing {ListingId}", l.Id);
            throw new DomainException("photo_store_unavailable", "This photo could not be uploaded. Try again.", 502);
        }

        var photo = new ListingPhoto
        {
            ListingId = l.Id, CloudinaryPublicId = stored.PublicId, Version = stored.Version, Width = stored.Width,
            Height = stored.Height, Bytes = stored.Bytes, CreatedAt = now,
            SortOrder = l.Photos.Count == 0 ? 0 : l.Photos.Max(p => p.SortOrder) + 1,
        };
        db.Photos.Add(photo);
        AuditPhotosIfLive(l, ownerId, now, "add", stored.PublicId);
        l.UpdatedAt = now;
        await Save(ct);
        return photo.ToPhotoDto();
    }

    public async Task<IReadOnlyList<PhotoDto>> ReorderPhotosAsync(Guid ownerId, string key, IReadOnlyList<Guid> order, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var l = await Find(ownerId, key, ct);
        if (!l.CanEdit(now)) throw new InvalidTransitionException(l.Status.ToValue(), "photos");
        if (order.Count != l.Photos.Count || !order.ToHashSet().SetEquals(l.Photos.Select(p => p.Id)))
            throw ValidationFailedException.For("order", "invalid");
        for (var i = 0; i < order.Count; i++) l.Photos.Single(p => p.Id == order[i]).SortOrder = i;
        AuditPhotosIfLive(l, ownerId, now, "reorder", null);
        l.UpdatedAt = now;
        await Save(ct);
        return l.Photos.OrderBy(p => p.SortOrder).Select(p => p.ToPhotoDto()).ToList();
    }

    /// <summary>Removes the row, then asks the store to destroy the file. A store failure is logged for manual clean-up.</summary>
    public async Task DeletePhotoAsync(Guid ownerId, string key, Guid photoId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var l = await Find(ownerId, key, ct);
        if (!l.CanEdit(now)) throw new InvalidTransitionException(l.Status.ToValue(), "photos");
        var photo = l.Photos.FirstOrDefault(p => p.Id == photoId) ?? throw new NotFoundException();
        if (l.Status == ListingStatus.Published && l.Photos.Count <= Listing.MinPhotos)
            throw new PhotosRequiredException(l.Photos.Count - 1);
        db.Photos.Remove(photo);
        l.Photos.Remove(photo);
        var n = 0;
        foreach (var p in l.Photos.OrderBy(p => p.SortOrder)) p.SortOrder = n++;
        AuditPhotosIfLive(l, ownerId, now, "remove", photo.CloudinaryPublicId);
        l.UpdatedAt = now;
        await Save(ct);
        try { await photos.DeleteAsync(photo.CloudinaryPublicId, ct); }
        catch (Exception e) when (e is PhotoStoreException or HttpRequestException)
        {
            log.LogWarning(e, "Photo {PublicId} was removed from the listing but not from the store; delete it manually", photo.CloudinaryPublicId);
        }
    }

    // ---------- Helpers ----------

    private void AuditPhotosIfLive(Listing l, Guid ownerId, DateTimeOffset now, string change, string? publicId)
    {
        if (l.Status == ListingStatus.Published)
            db.Audit(now, ownerId, "listing.photos", "listing", l.Id.ToString(), null, new { change, publicId });
    }

    private IQueryable<Listing> Load()
        => db.Listings.Include(l => l.Charges).Include(l => l.Photos).Include(l => l.Amenities).AsSplitQuery();

    /// <summary>Finds one of the owner's listings by id or short id. Other people's listings are "not found".</summary>
    private async Task<Listing> Find(Guid ownerId, string key, CancellationToken ct)
    {
        var byId = Guid.TryParse(key, out var id);
        return await Load().FirstOrDefaultAsync(l => l.OwnerId == ownerId && (byId ? l.Id == id : l.ShortId == key), ct)
               ?? throw new NotFoundException();
    }

    private async Task Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw Errors.Conflict("stale_listing", "This listing changed since you opened it. Reload and try again.");
        }
    }

    private async Task<string> NewShortId(CancellationToken ct)
    {
        while (true)
        {
            var id = ShortIds.New();
            if (!await db.Listings.AnyAsync(l => l.ShortId == id, ct)) return id;
        }
    }
}

public static class ShortIds
{
    private const string Alphabet = "23456789abcdefghjkmnpqrstuvwxyz";

    public static string New(int length = 6)
        => string.Create(length, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++) span[i] = Alphabet[System.Security.Cryptography.RandomNumberGenerator.GetInt32(Alphabet.Length)];
        });
}

public sealed class PhotoRejectedException(string message) : Exception(message);

/// <summary>Checks the file signature, not the name or the declared type.</summary>
public static class PhotoSignature
{
    public static async Task<bool> IsImageAsync(Stream s, CancellationToken ct)
    {
        var head = new byte[12];
        var read = 0;
        while (read < head.Length)
        {
            var n = await s.ReadAsync(head.AsMemory(read), ct);
            if (n == 0) break;
            read += n;
        }
        if (s.CanSeek) s.Position = 0; else throw new InvalidOperationException("Photo stream must be seekable.");
        if (read >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF) return true;
        if (read >= 8 && head.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return true;
        return read >= 12 && head.AsSpan(0, 4).SequenceEqual("RIFF"u8) && head.AsSpan(8, 4).SequenceEqual("WEBP"u8);
    }
}
