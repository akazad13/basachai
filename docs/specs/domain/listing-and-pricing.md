# Listing and pricing rules

Every listing shows each cost on its own line and a true monthly total, so a ৳10,500 rent never hides ৳2,280 of monthly bills. One-time costs are shown separately and never added to the total.

Version 1.0 · 13 October 2026 · Source: Master Spec v1.2, section 7 · Covers FR-05, FR-06, FR-22

## Fields

| Field | Required | Type | Rule |
| --- | --- | --- | --- |
| Title | Yes | Text, 10 to 90 characters | Plain text; HTML stripped |
| Purpose | System | `rent` or `sale` | Always `rent` in the MVP (FR-22) |
| Type | Yes | Configured list | `flat` or `house` (semi-pucca) at launch |
| Area | Yes | Location ID | One of the 5 pilot areas |
| Bedrooms | Yes | Integer 0 to 10 | 0 = studio |
| Bathrooms | Yes | Integer 1 to 10 | |
| Size | No | Integer, square feet | |
| Floor | No | Integer 0 to 50 | 0 = ground floor |
| Furnishing | Yes | `furnished`, `semi`, `unfurnished` | |
| Who can rent | Yes | `any`, `family`, `bachelor`, `students` | |
| Available from | Yes | Date or null | Null = now |
| Monthly rent | Yes | Whole taka, more than 0 | |
| Service charge | Yes | Whole taka, 0 or more | 0 shown as "None" |
| Advance | Yes | Whole taka, or "not specified" | |
| Other costs | No | List of name, amount (nullable), monthly or one-time | Null amount = varies, for example metered electricity |
| Amenities | No | Configured list | Lift, parking, generator, gas line, balcony, security guard, rooftop access, courtyard |
| Rules | No | Text, up to 300 characters | |
| Description | No | Text, up to 2,000 characters | |
| Full address | No | Text | Encrypted; never public; never in public API responses |
| Photos | Yes | 3 to 20 | First is the cover |

## Cost breakdown

### Calculation

```
monthly_total = rent
              + service_charge
              + sum(other costs where period = monthly and amount is known)
```

- Monthly costs with an unknown amount are listed (amount shown as "By meter") and excluded from the total, with the note "The total does not include {names}."
- One-time costs (advance and any one-time other costs) appear only in the "Paid once when you move in" box.
- The rent filter matches `rent`, never `monthly_total`.
- All amounts are whole taka stored as integers.

### Display

| Part | Rule |
| --- | --- |
| Heading | "Cost breakdown" |
| Lines | Rent; Service charge (or "None"); each monthly other cost. Name left, dotted leader, amount right |
| Total | Brass double rule above; "Total per month" and the amount in Baloo Da 2, 28 to 30 px |
| Unknown note | Muted line under the total when any monthly amount is unknown |
| One-time box | Brass tint; "Paid once when you move in"; Advance (or "Not specified"); one-time other costs |

### Worked example

| Line | Amount |
| --- | --- |
| Rent | ৳10,500 |
| Service charge | ৳1,200 |
| Gas bill (double burner) | ৳1,080 |
| **Total per month** | **৳12,780** |
| Advance (one-time) | ৳21,000 |
| Key deposit, refundable (one-time) | ৳1,000 |

### On cards

Total per month in large type; under it "Rent ৳10,500 + service ৳1,200 + 1 more bill(s)" and "Advance: ৳21,000" (or "Not specified").

## Validation messages

| Case | Message |
| --- | --- |
| Missing required field | "This field is required" |
| Rent 0 or empty | "This field is required" |
| Advance empty and not marked "not specified" | "This field is required" |
| Fewer than 3 photos | "Add {n} more photo(s) to submit" |

## Acceptance criteria

- **PRICE-1** Given rent 10,500, service 1,200 and a monthly gas bill 1,080, then the total is ৳12,780.
- **PRICE-2** Given a monthly electricity cost with no amount, then it shows "By meter", is excluded from the total, and the note names it.
- **PRICE-3** Given an advance of 21,000 and a one-time deposit of 1,000, then neither is in the total and both show in the one-time box.
- **PRICE-4** Given service charge 0, then the line shows "None".
- **PRICE-5** Given advance "not specified", then the one-time box shows "Not specified" and cards show "Advance: Not specified".

## Test cases

Pure domain rules, so these are unit tests (no database, no fakes needed). UI rendering of the same numbers is covered by PROP-TC01 and POST-TC02.

| ID | Covers | Case | Expected |
| --- | --- | --- | --- |
| PRICE-UT01 | PRICE-1 | Rent 10,500, service 1,200, monthly gas 1,080 | Monthly total 12,780 |
| PRICE-UT02 | PRICE-2 | Rent 6,000, service 0, monthly electricity with no amount | Total 6,000; electricity listed in `UnknownMonthly` |
| PRICE-UT03 | PRICE-3 | Advance 21,000 and one-time key deposit 1,000 | Neither in the total; both in `OneTime` in that order |
| PRICE-UT04 | PRICE-4 | Service charge 0 | `ServiceCharge.IsNone` is true |
| PRICE-UT05 | PRICE-5 | Advance not specified | `Advance.NotSpecified` is true; `OneTime` has one line with no amount |

### Automation

```csharp
// tests/BasaChai.Domain.Tests/CostBreakdownTests.cs
public class CostBreakdownTests
{
    [Fact] // PRICE-UT01
    public void Total_adds_rent_service_and_known_monthly_costs()
    {
        var costs = new CostBreakdown(rent: 10_500, serviceCharge: 1_200,
            others: [OtherCost.Monthly("Gas bill", 1_080)], advance: Advance.Of(21_000));
        Assert.Equal(12_780, costs.MonthlyTotal);
    }

    [Fact] // PRICE-UT02
    public void Unknown_monthly_amounts_are_listed_but_not_added()
    {
        var costs = new CostBreakdown(6_000, 0, [OtherCost.Monthly("Electricity bill", null)], Advance.Of(12_000));
        Assert.Equal(6_000, costs.MonthlyTotal);
        Assert.Equal(["Electricity bill"], costs.UnknownMonthly.Select(c => c.Label));
    }

    [Fact] // PRICE-UT03
    public void One_time_costs_never_enter_the_total()
    {
        var costs = new CostBreakdown(10_500, 1_200,
            [OtherCost.Monthly("Gas bill", 1_080), OtherCost.OneTime("Key deposit", 1_000)], Advance.Of(21_000));
        Assert.Equal(12_780, costs.MonthlyTotal);
        Assert.Equal([21_000, 1_000], costs.OneTime.Select(c => c.Amount!.Value));
    }

    [Fact] // PRICE-UT04
    public void Zero_service_charge_is_none()
        => Assert.True(new CostBreakdown(7_000, 0, [], Advance.Of(14_000)).ServiceCharge.IsNone);

    [Fact] // PRICE-UT05
    public void Advance_can_be_not_specified()
    {
        var costs = new CostBreakdown(4_500, 0, [], Advance.NotSpecified);
        Assert.True(costs.Advance.IsNotSpecified);
        Assert.Null(Assert.Single(costs.OneTime).Amount);
    }
}
```

## Related

[property page](../features/property-page.md) · [post a property](../features/post-property.md) · [data model](../tech/data-model.md)
