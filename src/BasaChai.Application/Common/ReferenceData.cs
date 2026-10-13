using BasaChai.Domain;
using Microsoft.EntityFrameworkCore;

namespace BasaChai.Application.Common;

/// <summary>Areas, property types and amenities. Small tables, cached in memory and reloaded after admin edits.</summary>
public sealed class ReferenceData
{
    public ReferenceData(IReadOnlyList<Location> locations, IReadOnlyList<PropertyType> types, IReadOnlyList<Amenity> amenities)
    {
        Locations = locations;
        Types = types;
        Amenities = amenities;
        LocationById = locations.ToDictionary(l => l.Id);
        TypeById = types.ToDictionary(t => t.Id);
        AmenityById = amenities.ToDictionary(a => a.Id);
    }

    public IReadOnlyList<Location> Locations { get; }
    public IReadOnlyList<PropertyType> Types { get; }
    public IReadOnlyList<Amenity> Amenities { get; }
    public IReadOnlyDictionary<int, Location> LocationById { get; }
    public IReadOnlyDictionary<int, PropertyType> TypeById { get; }
    public IReadOnlyDictionary<int, Amenity> AmenityById { get; }

    public IEnumerable<Location> ActiveAreas => Locations.Where(l => l.Kind == "area" && l.IsActive).OrderBy(l => l.Id);

    public Location? AreaBySlug(string? slug)
        => slug is null ? null : Locations.FirstOrDefault(l => l.Kind == "area" && l.Slug == slug && l.IsActive);

    public PropertyType? TypeByKey(string? key) => Types.FirstOrDefault(t => t.Key == key && t.IsActive);

    public Amenity? AmenityByKey(string key) => Amenities.FirstOrDefault(a => a.Key == key && a.IsActive);

    /// <summary>The location and everything under it, so filtering by an area also matches its sub-areas.</summary>
    public IReadOnlyList<int> SelfAndDescendants(Location root)
        => Locations.Where(l => l.Path == root.Path || l.Path.StartsWith(root.Path + ".", StringComparison.Ordinal))
                    .Select(l => l.Id).ToList();
}

public sealed class ReferenceCache
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ReferenceData? _data;
    private int _generation;

    public async Task<ReferenceData> GetAsync(IAppDbContext db, CancellationToken ct = default)
    {
        if (Volatile.Read(ref _data) is { } d) return d;
        await _gate.WaitAsync(ct);
        try
        {
            if (_data is { } loaded) return loaded;
            var generation = Volatile.Read(ref _generation);
            var fresh = new ReferenceData(
                await db.Locations.AsNoTracking().OrderBy(l => l.Id).ToListAsync(ct),
                await db.PropertyTypes.AsNoTracking().OrderBy(t => t.Id).ToListAsync(ct),
                await db.Amenities.AsNoTracking().OrderBy(a => a.Id).ToListAsync(ct));
            // An admin edit during the load makes this copy stale: use it for this request but do not keep it.
            if (generation == Volatile.Read(ref _generation)) _data = fresh;
            return fresh;
        }
        finally { _gate.Release(); }
    }

    public void Invalidate()
    {
        Interlocked.Increment(ref _generation);
        Volatile.Write(ref _data, null);
    }
}
