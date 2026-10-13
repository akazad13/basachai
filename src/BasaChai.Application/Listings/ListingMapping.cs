using BasaChai.Application.Common;
using BasaChai.Domain;
using BasaChai.Domain.Listings;
using BasaChai.Domain.Pricing;

namespace BasaChai.Application.Listings;

/// <summary>Explicit mapping (AutoMapper is banned, D21). Expects Charges, Photos and Amenities loaded.</summary>
public static class ListingMapping
{
    public static AreaDto? ToAreaDto(this Location? l) => l is null ? null : new AreaDto(l.Slug, l.NameBn, l.NameEn);

    public static CardDto ToCard(this Listing l, ReferenceData r)
    {
        var costs = l.Costs();
        var area = l.LocationId is { } lid && r.LocationById.TryGetValue(lid, out var loc) ? loc : null;
        var cover = l.Photos.OrderBy(p => p.SortOrder).FirstOrDefault();
        return new CardDto(
            l.ShortId, l.Slug, l.Title, area.ToAreaDto() ?? new AreaDto("", "", ""), TypeKey(l, r),
            l.Bedrooms, l.Bathrooms, l.FloorAreaSqft, l.AvailableFrom,
            costs.Rent, costs.ServiceCharge.Amount,
            costs.Monthly.Select(o => new MoneyLineDto(o.Label, o.Amount)).ToList(), costs.MonthlyTotal,
            new AdvanceDto(costs.Advance.Amount, costs.Advance.IsNotSpecified),
            cover is null ? null : new PhotoRefDto(cover.CloudinaryPublicId, cover.Version),
            l.Photos.Count, l.LastConfirmedAt, PhoneVerified: false);
    }

    public static ListingDetailDto ToDetail(this Listing l, ReferenceData r, string ownerName, ListingStatus effective)
    {
        var costs = l.Costs();
        return new ListingDetailDto(
            Available: effective == ListingStatus.Published && !l.IsHidden, effective.ToValue(), l.ToCard(r),
            l.Description, l.Rules, l.FloorNumber, l.Furnishing, l.Occupancy,
            l.Amenities.Select(a => r.AmenityById.GetValueOrDefault(a.AmenityId)).OfType<Amenity>()
                .Select(a => new NamedDto(a.Key, a.NameBn, a.NameEn)).ToList(),
            costs.OneTime.Select(c => new OneTimeLineDto(c.Label, c.Amount, c.IsAdvance)).ToList(),
            l.Photos.OrderBy(p => p.SortOrder).Select(ToPhotoDto).ToList(),
            new OwnerPublicDto(ownerName, l.AdvertiserType), l.IsHidden);
    }

    /// <summary>The shape for a listing that is not available to the public: no details at all.</summary>
    public static ListingDetailDto Unavailable(ListingStatus effective)
        => new(false, effective.ToValue(), null, null, null, null, null, null, [], [], [], null, false);

    public static OwnerListingDto ToOwnerDto(this Listing l, ReferenceData r, DateTimeOffset now, string? fullAddress)
    {
        var effective = l.EffectiveStatus(now);
        var cover = l.Photos.OrderBy(p => p.SortOrder).FirstOrDefault();
        var advance = l.Charges.FirstOrDefault(c => c.Kind == ChargeKind.Advance);
        int? daysLeft = effective is ListingStatus.Published or ListingStatus.Paused && l.ExpiresAt is { } e
            ? Math.Max(0, (int)Math.Ceiling((e - now).TotalDays))
            : null;
        return new OwnerListingDto(
            l.Id, l.ShortId, l.Title, effective.ToValue(), l.IsHidden, daysLeft, l.ViewsCount, l.RevealsCount,
            l.Costs().MonthlyTotal, cover is null ? null : new PhotoRefDto(cover.CloudinaryPublicId, cover.Version),
            effective is ListingStatus.Changes or ListingStatus.Rejected ? l.ModeratorReason : null,
            l.SubmittedAt, l.LastConfirmedAt,
            l.PropertyTypeId is { } t ? r.TypeById.GetValueOrDefault(t)?.Key : null,
            l.LocationId is { } a ? r.LocationById.GetValueOrDefault(a)?.Slug : null,
            l.Bedrooms, l.Bathrooms, l.FloorAreaSqft, l.FloorNumber, l.Furnishing, l.Occupancy, l.AvailableFrom,
            l.Description, l.Rules, fullAddress, l.MonthlyRent, l.ServiceCharge,
            advance is null ? null : new AdvanceDto(advance.Amount, advance.NotSpecified),
            l.Charges.Where(c => c.Kind != ChargeKind.Advance).OrderBy(c => c.SortOrder)
                .Select(c => new OtherCostInput(c.Label, c.Amount, c.Kind == ChargeKind.OtherMonthly ? "monthly" : "one_time")).ToList(),
            l.Amenities.Select(x => r.AmenityById.GetValueOrDefault(x.AmenityId)?.Key).OfType<string>().ToList(),
            l.Photos.OrderBy(p => p.SortOrder).Select(ToPhotoDto).ToList(), l.Version);
    }

    public static PhotoDto ToPhotoDto(this ListingPhoto p) => new(p.Id, p.CloudinaryPublicId, p.Version, p.Width, p.Height);

    public static string TypeKey(Listing l, ReferenceData r)
        => l.PropertyTypeId is { } t ? r.TypeById.GetValueOrDefault(t)?.Key ?? "" : "";

    /// <summary>A readable English slug built from the facts, since titles are usually typed in Bangla.</summary>
    public static string BuildSlug(Listing l, ReferenceData r)
    {
        var type = TypeKey(l, r) == "house" ? "semi-pucca-house" : "flat";
        var beds = l.Bedrooms == 0 ? "studio" : $"{l.Bedrooms}-bedroom";
        var area = l.LocationId is { } a ? r.LocationById.GetValueOrDefault(a)?.Slug : null;
        return area is null ? $"{beds}-{type}" : $"{beds}-{type}-in-{area}";
    }

    public static ChargeKind ParsePeriod(string period) => period switch
    {
        "monthly" => ChargeKind.OtherMonthly,
        "one_time" or "once" => ChargeKind.OtherOneTime,
        _ => throw ValidationFailedException.For("others", "invalid"),
    };
}
