using System.Text.Json;
using System.Text.RegularExpressions;
using BasaChai.Application.Common;
using BasaChai.Domain;
using BasaChai.Domain.Listings;
using BasaChai.Domain.Pricing;

namespace BasaChai.Application.Listings;

/// <summary>Applies a create or PATCH body to a listing, validating each field the body touches.</summary>
public static partial class ListingEditor
{
    private static readonly string[] Furnishings = ["furnished", "semi", "unfurnished"];
    private static readonly string[] Groups = ["any", "family", "bachelor", "students"];

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tags();

    public static string Plain(string s) => Tags().Replace(s, "").Trim();

    public static void Apply(Listing l, ListingInput i, ReferenceData r, IAddressProtector addresses)
    {
        var e = new Dictionary<string, string[]>();
        void Bad(string field, string key = "invalid") => e[field] = [key];

        if (i.Title is not null)
        {
            var t = Plain(i.Title);
            if (t.Length is 0 or > 90) Bad("title", "title_length"); else l.Title = t;
        }
        if (i.Type is not null)
        {
            if (r.TypeByKey(i.Type) is { } type) l.PropertyTypeId = type.Id;
            else if (!(l.PropertyTypeId is { } cur && r.TypeById.GetValueOrDefault(cur)?.Key == i.Type)) Bad("type");   // keeping a since-deactivated value is fine
        }
        if (i.Area is not null)
        {
            if (r.AreaBySlug(i.Area) is { } area) l.LocationId = area.Id;
            else if (!(l.LocationId is { } cur && r.LocationById.GetValueOrDefault(cur)?.Slug == i.Area)) Bad("area");
        }
        if (i.Bedrooms is { } beds) { if (beds is < 0 or > 10) Bad("bedrooms"); else l.Bedrooms = beds; }
        if (i.Bathrooms is { } baths) { if (baths is < 1 or > 10) Bad("bathrooms"); else l.Bathrooms = baths; }
        if (i.FloorAreaSqft is { } sqft) { if (sqft is < 1 or > 100_000) Bad("floorAreaSqft"); else l.FloorAreaSqft = sqft; }
        if (i.FloorNumber is { } floor) { if (floor is < 0 or > 50) Bad("floorNumber"); else l.FloorNumber = floor; }
        // A missing number means "leave as is", so optional numbers the owner emptied are named here.
        foreach (var field in i.Clear ?? [])
        {
            if (field == "floorAreaSqft") l.FloorAreaSqft = null;
            else if (field == "floorNumber") l.FloorNumber = null;
            else Bad("clear");
        }
        if (i.Furnishing is not null) { if (Furnishings.Contains(i.Furnishing)) l.Furnishing = i.Furnishing; else Bad("furnishing"); }
        if (i.Occupancy is not null) { if (Groups.Contains(i.Occupancy)) l.Occupancy = i.Occupancy; else Bad("occupancy"); }
        if (i.AvailableNow == true) l.AvailableFrom = null;
        else if (i.AvailableFrom is { } from) l.AvailableFrom = from;
        if (i.Description is not null)
        {
            var d = Plain(i.Description);
            if (d.Length > 2000) Bad("description", "too_long"); else l.Description = d.Length == 0 ? null : d;
        }
        if (i.Rules is not null)
        {
            var rules = Plain(i.Rules);
            if (rules.Length > 300) Bad("rules", "too_long"); else l.Rules = rules.Length == 0 ? null : rules;
        }
        if (i.FullAddress is not null)
        {
            var a = i.FullAddress.Trim();
            if (a.Length > 300) Bad("fullAddress", "too_long"); else l.FullAddressEnc = a.Length == 0 ? null : addresses.Protect(a);
        }
        if (i.MonthlyRent is { } rent) { if (rent is <= 0 or > 10_000_000) Bad("monthlyRent", "required"); else l.MonthlyRent = rent; }
        if (i.ServiceCharge is { } svc) { if (svc is < 0 or > 1_000_000) Bad("serviceCharge"); else l.ServiceCharge = svc; }
        if (i.Advance is { } adv)
        {
            if (!adv.NotSpecified && adv.Amount is null or <= 0) Bad("advance", "required");
            else
            {
                l.Charges.RemoveAll(c => c.Kind == ChargeKind.Advance);
                l.Charges.Add(new ListingCharge
                {
                    ListingId = l.Id, Kind = ChargeKind.Advance, Label = CostBreakdown.AdvanceLabel,
                    Amount = adv.NotSpecified ? null : adv.Amount, NotSpecified = adv.NotSpecified,
                });
            }
        }
        if (i.Others is { } others)
        {
            if (others.Count > 10 || others.Any(o => string.IsNullOrWhiteSpace(o.Label) || Plain(o.Label).Length > 60 || o.Amount is < 0 or > 1_000_000
                                                     || o.Period is not ("monthly" or "one_time")))
                Bad("others");
            else
            {
                l.Charges.RemoveAll(c => c.Kind != ChargeKind.Advance);
                l.Charges.AddRange(others.Select((o, n) => new ListingCharge
                {
                    ListingId = l.Id, Kind = ListingMapping.ParsePeriod(o.Period), Label = Plain(o.Label), Amount = o.Amount, SortOrder = n,
                }));
            }
        }
        if (i.Amenities is { } keys)
        {
            var current = l.Amenities.Select(a => r.AmenityById.GetValueOrDefault(a.AmenityId)).OfType<Amenity>().ToList();
            var found = keys.Distinct().Select(k => r.AmenityByKey(k) ?? current.FirstOrDefault(a => a.Key == k)).ToList();
            if (found.Any(a => a is null)) Bad("amenities");
            else
            {
                // Change only what differs, so a kept amenity is never deleted and re-added with the same key.
                var wanted = found.Select(a => a!.Id).ToHashSet();
                l.Amenities.RemoveAll(a => !wanted.Contains(a.AmenityId));
                foreach (var id in wanted.Where(id => l.Amenities.All(a => a.AmenityId != id)))
                    l.Amenities.Add(new ListingAmenity { ListingId = l.Id, AmenityId = id });
                l.AmenityText = string.Join(' ', found.Select(a => $"{a!.NameBn} {a.NameEn}"));
            }
        }

        if (e.Count > 0) throw new ValidationFailedException(e);
        l.Slug = ListingMapping.BuildSlug(l, r);
    }

    /// <summary>The editable fields as a flat map, for before and after audit entries. The address is never included.</summary>
    public static Dictionary<string, JsonElement> Snapshot(Listing l, ReferenceData r, IAddressProtector addresses)
    {
        var o = new Dictionary<string, object?>
        {
            ["title"] = l.Title, ["type"] = ListingMapping.TypeKey(l, r),
            ["area"] = l.LocationId is { } a ? r.LocationById.GetValueOrDefault(a)?.Slug : null,
            ["bedrooms"] = l.Bedrooms, ["bathrooms"] = l.Bathrooms, ["floorAreaSqft"] = l.FloorAreaSqft,
            ["floorNumber"] = l.FloorNumber, ["furnishing"] = l.Furnishing, ["occupancy"] = l.Occupancy,
            ["availableFrom"] = l.AvailableFrom, ["description"] = l.Description, ["rules"] = l.Rules,
            ["monthlyRent"] = l.MonthlyRent, ["serviceCharge"] = l.ServiceCharge,
            ["advance"] = l.Charges.Where(c => c.Kind == ChargeKind.Advance).Select(c => new { c.Amount, c.NotSpecified }).FirstOrDefault(),
            ["others"] = l.Charges.Where(c => c.Kind != ChargeKind.Advance).OrderBy(c => c.SortOrder)
                .Select(c => new { c.Label, c.Amount, period = c.Kind == ChargeKind.OtherMonthly ? "monthly" : "one_time" }).ToList(),
            ["amenities"] = l.Amenities.Select(x => r.AmenityById.GetValueOrDefault(x.AmenityId)?.Key).Order().ToList(),
            ["photos"] = l.Photos.OrderBy(p => p.SortOrder).Select(p => p.CloudinaryPublicId).ToList(),
            // A short fingerprint shows that the address changed without putting the address in the audit log.
            ["fullAddress"] = l.FullAddressEnc is null ? null
                : Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(addresses.Unprotect(l.FullAddressEnc))))[..10],
        };
        return o.ToDictionary(kv => kv.Key, kv => JsonSerializer.SerializeToElement(kv.Value, Json.Web));
    }

    /// <summary>Only the fields that changed, as (before, after).</summary>
    public static (Dictionary<string, JsonElement> Before, Dictionary<string, JsonElement> After) Diff(
        Dictionary<string, JsonElement> before, Dictionary<string, JsonElement> after)
    {
        var keys = before.Keys.Where(k => before[k].GetRawText() != after[k].GetRawText()).ToList();
        return (keys.ToDictionary(k => k, k => before[k]), keys.ToDictionary(k => k, k => after[k]));
    }
}
