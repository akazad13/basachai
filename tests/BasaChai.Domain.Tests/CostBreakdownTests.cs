using BasaChai.Domain.Pricing;

namespace BasaChai.Domain.Tests;

public class CostBreakdownTests
{
    [Fact] // PRICE-UT01
    public void PRICE_UT01_Total_adds_rent_service_and_known_monthly_costs()
    {
        var costs = new CostBreakdown(rent: 10_500, serviceCharge: 1_200,
            others: [OtherCost.Monthly("Gas bill", 1_080)], advance: Advance.Of(21_000));
        Assert.Equal(12_780, costs.MonthlyTotal);
    }

    [Fact] // PRICE-UT02
    public void PRICE_UT02_Unknown_monthly_amounts_are_listed_but_not_added()
    {
        var costs = new CostBreakdown(6_000, 0, [OtherCost.Monthly("Electricity bill", null)], Advance.Of(12_000));
        Assert.Equal(6_000, costs.MonthlyTotal);
        Assert.Equal(["Electricity bill"], costs.UnknownMonthly.Select(c => c.Label));
    }

    [Fact] // PRICE-UT03
    public void PRICE_UT03_One_time_costs_never_enter_the_total()
    {
        var costs = new CostBreakdown(10_500, 1_200,
            [OtherCost.Monthly("Gas bill", 1_080), OtherCost.OneTime("Key deposit", 1_000)], Advance.Of(21_000));
        Assert.Equal(12_780, costs.MonthlyTotal);
        Assert.Equal([21_000, 1_000], costs.OneTime.Select(c => c.Amount!.Value));
    }

    [Fact] // PRICE-UT04
    public void PRICE_UT04_Zero_service_charge_is_none()
        => Assert.True(new CostBreakdown(7_000, 0, [], Advance.Of(14_000)).ServiceCharge.IsNone);

    [Fact] // PRICE-UT05
    public void PRICE_UT05_Advance_can_be_not_specified()
    {
        var costs = new CostBreakdown(4_500, 0, [], Advance.NotSpecified);
        Assert.True(costs.Advance.IsNotSpecified);
        Assert.Null(Assert.Single(costs.OneTime).Amount);
    }
}
