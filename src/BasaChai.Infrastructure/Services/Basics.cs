using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using BasaChai.Application.Common;
using Microsoft.AspNetCore.DataProtection;

namespace BasaChai.Infrastructure.Services;

public sealed class AddressProtector(IDataProtectionProvider provider) : IAddressProtector
{
    private readonly IDataProtector _p = provider.CreateProtector("BasaChai.Listing.FullAddress.v1");
    public byte[] Protect(string address) => _p.Protect(Encoding.UTF8.GetBytes(address));
    public string Unprotect(byte[] data) => Encoding.UTF8.GetString(_p.Unprotect(data));
}

/// <summary>Sliding-window counters in memory. Uses the app's TimeProvider so tests can move time.</summary>
public sealed class InMemoryRateLimiter(TimeProvider clock) : IRateLimiter
{
    private static readonly TimeSpan LongestWindow = TimeSpan.FromDays(1);
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _hits = new();
    private int _calls;

    public TimeSpan? TryHit(string bucket, string key, int limit, TimeSpan window) => Hit(bucket, key, limit, window, record: true);

    public TimeSpan? Check(string bucket, string key, int limit, TimeSpan window) => Hit(bucket, key, limit, window, record: false);

    public void Refund(string bucket, string key)
    {
        if (!_hits.TryGetValue(bucket + "|" + key, out var q)) return;
        lock (q)
        {
            if (q.Count == 0) return;
            var kept = q.ToArray()[..^1];
            q.Clear();
            foreach (var t in kept) q.Enqueue(t);
        }
    }

    private TimeSpan? Hit(string bucket, string key, int limit, TimeSpan window, bool record)
    {
        var now = clock.GetUtcNow();
        if (Interlocked.Increment(ref _calls) % 1000 == 0) Sweep(now);
        var id = bucket + "|" + key;
        if (!record && !_hits.ContainsKey(id)) return null;   // checking never creates entries
        var q = _hits.GetOrAdd(id, _ => new Queue<DateTimeOffset>());
        lock (q)
        {
            while (q.Count > 0 && q.Peek() <= now - window) q.Dequeue();
            // Wait until enough old hits fall out of the window to make room for one more.
            if (q.Count >= limit) return q.ElementAt(q.Count - limit) + window - now;
            if (record) q.Enqueue(now);
            return null;
        }
    }

    public void Reset() => _hits.Clear();

    /// <summary>Drops keys with no hit inside the longest window, so memory stays bounded on the small VPS.</summary>
    private void Sweep(DateTimeOffset now)
    {
        foreach (var (id, q) in _hits)
        {
            bool idle;
            lock (q) idle = q.Count == 0 || q.Last() <= now - LongestWindow;
            if (idle) _hits.TryRemove(id, out _);
        }
    }
}

/// <summary>System time plus an offset that the Test and Staging clock hook can move.</summary>
public sealed class AdjustableClock : TimeProvider, IClockControl
{
    private TimeSpan _offset;
    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + _offset;
    public void Advance(TimeSpan by) => _offset += by;
    public void Reset() => _offset = TimeSpan.Zero;
}
