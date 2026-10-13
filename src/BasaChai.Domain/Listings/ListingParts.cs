namespace BasaChai.Domain.Listings;

public enum ChargeKind { Advance, OtherMonthly, OtherOneTime }

public class ListingCharge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ListingId { get; set; }
    public ChargeKind Kind { get; set; }
    public string Label { get; set; } = "";
    /// <summary>Null when the amount varies (other costs) or is not stated (advance).</summary>
    public int? Amount { get; set; }
    public bool NotSpecified { get; set; }
    public int SortOrder { get; set; }
}

public class ListingPhoto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ListingId { get; set; }
    public string CloudinaryPublicId { get; set; } = "";
    public long Version { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long Bytes { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class ListingAmenity
{
    public Guid ListingId { get; set; }
    public int AmenityId { get; set; }
}
