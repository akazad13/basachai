using System.Net.Http.Json;
using System.Threading.Channels;
using BasaChai.Application.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BasaChai.Infrastructure.Services;

/// <summary>
/// Asks the web app (Web:RevalidateUrl, signed with Web:RevalidateSecret) to drop cached pages. Normally through an
/// in-process queue with one retry, so saves never wait on the web host; inline where tests need the next page fresh.
/// </summary>
public sealed class WebPageCache(HttpClient http, string url, string secret, bool inline, ILogger<WebPageCache> log)
    : BackgroundService, IPageCache
{
    private readonly Channel<(string[] Listings, bool Everything)> _queue =
        Channel.CreateBounded<(string[], bool)>(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });

    public async Task InvalidateAsync(IReadOnlyCollection<string> listingShortIds, bool everything = false)
    {
        if (listingShortIds.Count == 0 && !everything) return;
        if (inline) await SendAsync([.. listingShortIds], everything);
        else _queue.Writer.TryWrite(([.. listingShortIds], everything));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (listings, everything) in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            if (await SendAsync(listings, everything)) continue;
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            await SendAsync(listings, everything);   // one retry; the pages refresh within minutes anyway
        }
    }

    private async Task<bool> SendAsync(string[] listings, bool everything)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(new { listings, all = everything }) };
            request.Headers.Add("X-Revalidate-Secret", secret);
            using var response = await http.SendAsync(request);
            if (response.IsSuccessStatusCode) return true;
            log.LogWarning("The web app refused a page purge: {Status}", (int)response.StatusCode);
        }
        catch (Exception e)
        {
            log.LogWarning(e, "Could not reach the web app to purge cached pages");
        }
        return false;
    }
}

/// <summary>When no web app is configured to purge (API-only runs): pages expire on their own.</summary>
public sealed class NoPageCache : IPageCache
{
    public Task InvalidateAsync(IReadOnlyCollection<string> listingShortIds, bool everything = false) => Task.CompletedTask;
}
