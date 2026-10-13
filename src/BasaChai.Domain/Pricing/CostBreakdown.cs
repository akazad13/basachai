namespace BasaChai.Domain.Pricing;

public enum CostPeriod { Monthly, OneTime }

/// <summary>An extra cost the owner lists. A null amount means it varies, for example metered electricity.</summary>
public sealed record OtherCost(string Label, int? Amount, CostPeriod Period)
{
    public static OtherCost Monthly(string label, int? amount) => new(label, amount, CostPeriod.Monthly);
    public static OtherCost OneTime(string label, int? amount) => new(label, amount, CostPeriod.OneTime);
}

/// <summary>Advance paid once when moving in, or "not specified" when the owner chose not to state it.</summary>
public sealed record Advance(int? Amount)
{
    public static Advance Of(int amount) => new(amount);
    public static readonly Advance NotSpecified = new((int?)null);
    public bool IsNotSpecified => Amount is null;
}

public sealed record ServiceCharge(int Amount)
{
    public bool IsNone => Amount == 0;
}

/// <summary>A line in the "Paid once when you move in" box. The advance line has <see cref="IsAdvance"/> set.</summary>
public sealed record CostLine(string Label, int? Amount, bool IsAdvance = false);

/// <summary>
/// Monthly total = rent + service charge + monthly costs with a known amount.
/// One-time costs and the advance are never part of the total.
/// </summary>
public sealed class CostBreakdown
{
    public const string AdvanceLabel = "advance";

    public CostBreakdown(int rent, int serviceCharge, IReadOnlyList<OtherCost> others, Advance advance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rent);
        ArgumentOutOfRangeException.ThrowIfNegative(serviceCharge);
        Rent = rent;
        ServiceCharge = new ServiceCharge(serviceCharge);
        Advance = advance;
        Monthly = others.Where(o => o.Period == CostPeriod.Monthly).ToList();
        UnknownMonthly = Monthly.Where(o => o.Amount is null).ToList();
        OneTime = [new CostLine(AdvanceLabel, advance.Amount, IsAdvance: true),
                   .. others.Where(o => o.Period == CostPeriod.OneTime).Select(o => new CostLine(o.Label, o.Amount))];
        MonthlyTotal = rent + serviceCharge + Monthly.Sum(o => o.Amount ?? 0);
    }

    public int Rent { get; }
    public ServiceCharge ServiceCharge { get; }
    public Advance Advance { get; }
    public IReadOnlyList<OtherCost> Monthly { get; }
    public IReadOnlyList<OtherCost> UnknownMonthly { get; }
    public IReadOnlyList<CostLine> OneTime { get; }
    public int MonthlyTotal { get; }
}
