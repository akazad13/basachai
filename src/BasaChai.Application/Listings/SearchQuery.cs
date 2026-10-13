using System.Globalization;
using System.Text;
using System.Text.Json;

namespace BasaChai.Application.Listings;

/// <summary>
/// Search filters from the URL. Unknown keys and invalid values are ignored rather than rejected,
/// so a hand-edited or old link still shows results for the filters that make sense.
/// </summary>
public sealed record SearchQuery
{
    public static readonly string[] Sorts = ["confirmed", "newest", "rent_asc", "rent_desc"];
    private static readonly string[] Furnishings = ["furnished", "semi", "unfurnished"];
    private static readonly string[] Groups = ["family", "bachelor", "students"];
    public const int DefaultSize = 20;
    public const int MaxSize = 24;

    public string? Area { get; init; }
    public int? Min { get; init; }
    public int? Max { get; init; }
    public int? Beds { get; init; }
    public int? Baths { get; init; }
    public string? Type { get; init; }
    public string? Furnish { get; init; }
    public string? Occ { get; init; }
    public string? Avail { get; init; }
    public string? Q { get; init; }
    public string Sort { get; init; } = "confirmed";
    public int Size { get; init; } = DefaultSize;
    public string? Cursor { get; init; }

    public static SearchQuery Parse(Func<string, string?> get)
    {
        static int? Int(string? s, int min, int max)
            => int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var v) && v >= min && v <= max ? v : null;
        static string? OneOf(string? s, string[] allowed) => s is not null && allowed.Contains(s) ? s : null;
        static string? Slug(string? s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 40 && s.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '-' or '_') ? s : null;

        var q = get("q")?.Trim();
        return new SearchQuery
        {
            Area = Slug(get("area")),
            Min = Int(get("min"), 0, 10_000_000),
            Max = Int(get("max"), 0, 10_000_000),
            Beds = Int(get("beds"), 0, 10),
            Baths = Int(get("baths"), 1, 10),
            Type = Slug(get("type")),
            Furnish = OneOf(get("furnish"), Furnishings),
            Occ = OneOf(get("occ"), Groups),
            Avail = OneOf(get("avail"), ["now", "month"]),
            Q = string.IsNullOrEmpty(q) ? null : q[..Math.Min(q.Length, 100)],
            Sort = OneOf(get("sort"), Sorts) ?? "confirmed",
            Size = Int(get("size"), 1, MaxSize) ?? DefaultSize,
            Cursor = get("cursor"),
        };
    }

    /// <summary>The filters in use, for the search_performed event.</summary>
    public Dictionary<string, object> Filters()
    {
        var f = new Dictionary<string, object>();
        if (Area is not null) f["area"] = Area;
        if (Min is not null) f["min"] = Min;
        if (Max is not null) f["max"] = Max;
        if (Beds is not null) f["beds"] = Beds;
        if (Baths is not null) f["baths"] = Baths;
        if (Type is not null) f["type"] = Type;
        if (Furnish is not null) f["furnish"] = Furnish;
        if (Occ is not null) f["occ"] = Occ;
        if (Avail is not null) f["avail"] = Avail;
        if (Q is not null) f["q"] = true;   // whether keywords were used; the text itself is free text and not stored
        return f;
    }
}

/// <summary>Keyset cursor: the sort, the last row's sort value and its short id as a tie-breaker.</summary>
public sealed record SearchCursor(string S, long V, string K)
{
    public string Encode() => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(this))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static SearchCursor? Decode(string? value, string sort)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 200) return null;
        try
        {
            var b64 = value.Replace('-', '+').Replace('_', '/');
            b64 += new string('=', (4 - b64.Length % 4) % 4);
            var c = JsonSerializer.Deserialize<SearchCursor>(Encoding.UTF8.GetString(Convert.FromBase64String(b64)));
            // A tampered value must not reach the query: timestamps outside DateTimeOffset's range would throw.
            return c is not null && c.S == sort && !string.IsNullOrEmpty(c.K) && c.K.Length <= 12
                   && c.V >= 0 && c.V <= DateTimeOffset.MaxValue.UtcTicks ? c : null;
        }
        catch (Exception e) when (e is FormatException or JsonException) { return null; }
    }
}
