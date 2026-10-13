# Analytics and metrics

Six kinds of events feed five pilot metrics. Events are written in the same request as the action, and admin metrics are computed by SQL when the dashboard opens (no nightly rollups, D12).

Version 1.0 · 13 October 2026 · Covers FR-20

## Events

| Event | Recorded by | Properties |
| --- | --- | --- |
| `search_performed` | API | `filters` (object), `sort`, `resultCount`, `source` (`home`, `search`) |
| `listing_viewed` | Web, via `POST /events` | `listingId`, `referrer` (`search`, `home`, `saved`, `direct`) |
| `contact_revealed` | API | `listingId`, `signedIn` |
| `listing_submitted` | API | `listingId`, `first` (bool) |
| `listing_published` | API | `listingId`, `hoursInQueue` |
| `listing_reconfirmed` | API | `listingId`, `daysSinceConfirm`, `wasExpired` |
| `listing_expired` | API (when expiry is persisted) | `listingId` |
| `report_created` | API | `listingId`, `reason`, `anonymous` |

- Every event has `name`, `session_id`, `user_id` (when signed in) and `created_at`.
- No phone numbers, addresses or free text in event properties.
- `listing_viewed` is rate-limited per session so refreshes do not inflate counts.

## Pilot metrics

| Metric | Definition | Target |
| --- | --- | --- |
| Listing completeness | Share of Live listings with every required field and at least 3 photos | At least 85% |
| Freshness | Share of sampled Live listings confirmed available within 30 days | At least 80% |
| Search to contact | Sessions with a `contact_revealed` divided by sessions with a `search_performed` | Baseline in month 1, then improve monthly. Session ids are hashed with the monthly salt, so a visit spanning midnight at the month's end counts as two sessions: a negligible error |
| Owner activation | Registered owners who publish a first listing | Track |
| Report handling | Share of reports acknowledged within one business day | 100% |

## Admin dashboard

Shows the counts in [moderation and admin](../features/moderation-and-admin.md#summary-counts), the five metrics for the last 30 days, and Cloudinary usage. Trigger to add rollups: dashboard queries over 2 seconds.

## Acceptance criteria

- **ANA-1** Given a search from the home page, then one `search_performed` event with `source: home` and the real result count is stored.
- **ANA-2** Given a phone reveal while signed out, then a `contact_revealed` event with `signedIn: false` is stored.
- **ANA-3** Given any event, then its properties contain no phone number or address.

## Test cases

| ID | Covers | Case | Expected |
| --- | --- | --- | --- |
| ANA-API01 | ANA-1 | `GET /v1/listings?area=dokkhin` with header `X-Search-Source: home` | One `search_performed` event with `resultCount` 3, `source` "home" and the filters |
| ANA-API02 | ANA-2 | Anonymous reveal on D3 | One `contact_revealed` event with `listingId` D3 and `signedIn` false |
| ANA-API03 | ANA-3 | Run a search, a reveal, a report with a note and a submission; read every stored event | No event's `props` contains a phone number, address or report note |

### Automation

```csharp
// tests/BasaChai.Api.Tests/AnalyticsTests.cs
public class AnalyticsTests(ApiFactory f) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => f.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact] // ANA-API01
    public async Task Search_records_source_count_and_filters()
    {
        var anon = await f.AsAsync("anonymous");
        anon.DefaultRequestHeaders.Add("X-Search-Source", "home");
        await anon.GetAsync("/v1/listings?area=dokkhin");
        var e = await f.LastEventAsync("search_performed");
        Assert.Equal(3, e.Props.GetProperty("resultCount").GetInt32());
        Assert.Equal("home", e.Props.GetProperty("source").GetString());
        Assert.Equal("dokkhin", e.Props.GetProperty("filters").GetProperty("area").GetString());
    }

    [Fact] // ANA-API02
    public async Task Reveal_records_signed_in_flag()
    {
        var anon = await f.AsAsync("anonymous");
        await anon.PostAsync("/v1/listings/D3/contact", null);
        var e = await f.LastEventAsync("contact_revealed");
        Assert.Equal("D3", e.Props.GetProperty("listingId").GetString());
        Assert.False(e.Props.GetProperty("signedIn").GetBoolean());
    }

    [Fact] // ANA-API03
    public async Task Events_never_hold_personal_data()
    {
        var anon = await f.AsAsync("anonymous");
        await anon.GetAsync("/v1/listings?q=gas");
        await anon.PostAsync("/v1/listings/D3/contact", null);
        await anon.PostAsJsonAsync("/v1/listings/B2/reports", new { Reason = "inaccurate", Details = "Secret note 01711111111" });
        await using var db = f.Db();
        var all = string.Join('\n', (await db.AnalyticsEvents.ToListAsync()).Select(e => e.Props.RootElement.GetRawText()));   // EF cannot translate GetRawText
        Assert.DoesNotMatch(@"(\+?880|0)1[3-9]\d{8}", all);
        Assert.DoesNotContain("Secret note", all);
        foreach (var address in TestData.FullAddresses) Assert.DoesNotContain(address, all);
    }
}
```

`f.LastEventAsync(name)` reads the newest `analytics_events` row with that name.

## Related

[requirements](../requirements.md) · [data model](data-model.md)
