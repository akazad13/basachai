using System.Text;
using BasaChai.Application.Identity;
using BasaChai.Domain;
using Microsoft.EntityFrameworkCore;

namespace BasaChai.Infrastructure.Data;

/// <summary>Seeds roles, the Dhamrai area tree with spelling aliases, property types and amenities. Safe to run on every start.</summary>
public static class ReferenceSeeder
{
    private static readonly (string Slug, string Bn, string En, string[] Aliases)[] Areas =
    [
        ("amin", "আমিন মডেল টাউন", "Amin Model Town", ["Amin", "Amin Town", "আমিন টাউন"]),
        ("barat", "বরাত নগর", "Barat Nagor", ["Barat Nagar", "Borat Nagor", "বরাতনগর"]),
        ("dokkhin", "দক্ষিণ পাড়া", "Dokkhin Para", ["Dakkhin Para", "Dakshin Para", "Dokkhinpara", "দক্ষিণপাড়া"]),
        ("bazar", "ধামরাই বাজার", "Dhamrai Bazar", ["Dhamrai Bazaar", "Dhamrai Market", "বাজার"]),
        ("islampur", "ইসলামপুর", "Islampur", ["Islampoor", "Eslampur"]),
    ];

    private static readonly (string Key, string Bn, string En)[] Types =
    [
        ("flat", "ফ্ল্যাট", "Flat"),
        ("house", "আধাপাকা বাড়ি", "Semi-pucca house"),
    ];

    private static readonly (string Key, string Bn, string En)[] AmenityList =
    [
        ("lift", "লিফট", "Lift"), ("parking", "পার্কিং", "Parking"), ("generator", "জেনারেটর", "Generator"),
        ("gas", "গ্যাস সংযোগ", "Gas line"), ("balcony", "বারান্দা", "Balcony"), ("security", "নিরাপত্তা প্রহরী", "Security guard"),
        ("roof", "ছাদ ব্যবহার", "Rooftop access"), ("yard", "উঠান", "Courtyard"),
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        foreach (var role in Roles.All)
            if (!await db.Roles.AnyAsync(r => r.Name == role, ct))
                db.Roles.Add(new AppRole(role) { NormalizedName = role.ToUpperInvariant(), ConcurrencyStamp = Guid.NewGuid().ToString() });

        if (!await db.Locations.AnyAsync(ct))
        {
            var division = new Location { Path = "dhaka", Kind = "division", NameBn = "ঢাকা বিভাগ", NameEn = "Dhaka Division", Slug = "dhaka" };
            var district = new Location { Path = "dhaka.dhaka_district", Kind = "district", NameBn = "ঢাকা জেলা", NameEn = "Dhaka District", Slug = "dhaka-district" };
            var upazila = new Location { Path = "dhaka.dhaka_district.dhamrai", Kind = "upazila", NameBn = "ধামরাই", NameEn = "Dhamrai", Slug = "dhamrai" };
            db.Locations.AddRange(division, district, upazila);
            await db.SaveChangesAsync(ct);
            district.ParentId = division.Id;
            upazila.ParentId = district.Id;
            foreach (var (slug, bn, en, aliases) in Areas)
            {
                var area = new Location { ParentId = upazila.Id, Path = $"{upazila.Path}.{slug}", Kind = "area", NameBn = bn, NameEn = en, Slug = slug };
                foreach (var alias in aliases.Append(bn).Append(en))
                    area.Aliases.Add(new LocationAlias { Alias = alias, Normalized = Normalize(alias) });
                db.Locations.Add(area);
            }
        }

        foreach (var (key, bn, en) in Types)
            if (!await db.PropertyTypes.AnyAsync(t => t.Key == key, ct)) db.PropertyTypes.Add(new PropertyType { Key = key, NameBn = bn, NameEn = en });
        foreach (var (key, bn, en) in AmenityList)
            if (!await db.Amenities.AnyAsync(a => a.Key == key, ct)) db.Amenities.Add(new Amenity { Key = key, NameBn = bn, NameEn = en });

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Lower case, no spaces or punctuation, so "Dakkhin-Para" and "dakkhin para" compare equal.</summary>
    public static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.Normalize(NormalizationForm.FormC).ToLowerInvariant())
            if (char.IsLetterOrDigit(c) || char.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.NonSpacingMark or System.Globalization.UnicodeCategory.SpacingCombiningMark)
                sb.Append(c);
        return sb.ToString();
    }
}
