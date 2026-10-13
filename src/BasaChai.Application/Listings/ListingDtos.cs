namespace BasaChai.Application.Listings;

public sealed record AreaDto(string Slug, string NameBn, string NameEn);
public sealed record MoneyLineDto(string Label, int? Amount);
public sealed record OneTimeLineDto(string Label, int? Amount, bool IsAdvance);
public sealed record AdvanceDto(int? Amount, bool NotSpecified);
public sealed record PhotoRefDto(string PublicId, long Version);
public sealed record PhotoDto(Guid Id, string PublicId, long Version, int Width, int Height);
public sealed record NamedDto(string Key, string NameBn, string NameEn);
public sealed record OwnerPublicDto(string DisplayName, string Type);

/// <summary>A listing card for search, home and saved homes. No phone, no address.</summary>
public sealed record CardDto(
    string ShortId, string Slug, string Title, AreaDto Area, string Type,
    int Bedrooms, int? Bathrooms, int? FloorAreaSqft, DateOnly? AvailableFrom,
    int Rent, int ServiceCharge, IReadOnlyList<MoneyLineDto> MonthlyOthers, int MonthlyTotal,
    AdvanceDto Advance, PhotoRefDto? Cover, int PhotoCount, DateTimeOffset? LastConfirmedAt, bool PhoneVerified);

public sealed record SearchPage(IReadOnlyList<CardDto> Items, int Count, string? NextCursor);

/// <summary>
/// The property page. <see cref="Available"/> is false for listings that are not Live; then only the
/// identity fields are filled unless the viewer is the owner or staff (preview with <see cref="Status"/>).
/// </summary>
public sealed record ListingDetailDto(
    bool Available, string Status, CardDto? Card, string? Description, string? Rules, int? FloorNumber,
    string? Furnishing, string? Occupancy, IReadOnlyList<NamedDto> Amenities, IReadOnlyList<OneTimeLineDto> OneTimeCosts,
    IReadOnlyList<PhotoDto> Photos, OwnerPublicDto? Owner, bool IsHidden);

public sealed record ContactDto(string Phone);

/// <summary>The home page cost example: a live card plus its one-time costs.</summary>
public sealed record CostExampleDto(CardDto Card, IReadOnlyList<OneTimeLineDto> OneTimeCosts);

public sealed record OtherCostInput(string Label, int? Amount, string Period);
public sealed record AdvanceInput(int? Amount, bool NotSpecified);

/// <summary>Create and PATCH body. Null means "leave as is"; empty strings clear optional text.</summary>
public sealed record ListingInput(
    string? Title = null, string? Type = null, string? Area = null, int? Bedrooms = null, int? Bathrooms = null,
    int? FloorAreaSqft = null, int? FloorNumber = null, string? Furnishing = null, string? Occupancy = null,
    DateOnly? AvailableFrom = null, bool? AvailableNow = null, string? Description = null, string? Rules = null,
    string? FullAddress = null, int? MonthlyRent = null, int? ServiceCharge = null, AdvanceInput? Advance = null,
    IReadOnlyList<OtherCostInput>? Others = null, IReadOnlyList<string>? Amenities = null,
    IReadOnlyList<string>? Clear = null);

public sealed record OwnerListingDto(
    Guid Id, string ShortId, string Title, string Status, bool IsHidden, int? DaysLeft, int Views, int Reveals,
    int MonthlyTotal, PhotoRefDto? Cover, string? ModeratorReason, DateTimeOffset? SubmittedAt, DateTimeOffset? LastConfirmedAt,
    string? Type, string? Area, int Bedrooms, int? Bathrooms, int? FloorAreaSqft, int? FloorNumber,
    string Furnishing, string Occupancy, DateOnly? AvailableFrom, string? Description, string? Rules, string? FullAddress,
    int? MonthlyRent, int? ServiceCharge, AdvanceDto? Advance, IReadOnlyList<OtherCostInput> Others,
    IReadOnlyList<string> Amenities, IReadOnlyList<PhotoDto> Photos, int Version);
