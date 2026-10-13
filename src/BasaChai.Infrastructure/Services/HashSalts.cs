using System.Security.Cryptography;
using System.Text;
using BasaChai.Application.Common;
using BasaChai.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BasaChai.Infrastructure.Services;

/// <summary>
/// The rotating salt for IP, device and session hashes that are written to the database (security spec, Privacy).
/// Each calendar month (UTC) gets a fresh random salt in <c>hash_salts</c>, so hashes stay comparable within the
/// month across restarts. The hashing key combines it with the server secret (<c>Hashing:Salt</c>), so neither a
/// database copy nor the secret alone is enough to test guesses. Salts older than the previous month are deleted,
/// and backups leave the table out, after which those months' hashes cannot be linked to anyone, even by us.
/// </summary>
public sealed class HashSalts(IServiceScopeFactory scopes, TimeProvider clock, byte[] secret)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile Current? _current;

    private sealed record Current(DateTimeOffset MonthStart, DateTimeOffset NextMonth, byte[] Key);

    public static string PeriodOf(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

    private static DateTimeOffset MonthStart(DateTimeOffset time) => new(time.UtcDateTime.Year, time.UtcDateTime.Month, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The key for this month. Requests call <see cref="EnsureAsync"/> first, so this is normally cached; the blocking
    /// load is only for a month boundary between that call and this one.
    /// </summary>
    public byte[] CurrentKey()
    {
        var now = clock.GetUtcNow();
        if (_current is { } c && now >= c.MonthStart && now < c.NextMonth) return c.Key;
        return EnsureAsync().GetAwaiter().GetResult();
    }

    /// <summary>Loads (or creates) this month's salt, and deletes salts older than last month. Cheap once loaded.</summary>
    public async Task<byte[]> EnsureAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        if (_current is { } c && now >= c.MonthStart && now < c.NextMonth) return c.Key;
        await _gate.WaitAsync(ct);
        try
        {
            var start = MonthStart(now);
            if (_current is { } again && again.MonthStart == start) return again.Key;
            string period = PeriodOf(start), previous = PeriodOf(start.AddMonths(-1));
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Concurrent first requests (or a second process) must agree on one salt: insert if absent, then read.
            var fresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO hash_salts (period, salt, created_at) VALUES ({period}, {fresh}, {now}) ON CONFLICT (period) DO NOTHING", ct);
            var salt = await db.HashSalts.Where(s => s.Period == period).Select(s => s.Salt).SingleAsync(ct);
            await db.HashSalts.Where(s => s.Period != period && s.Period != previous).ExecuteDeleteAsync(ct);

            var key = HMACSHA256.HashData(secret, Convert.FromBase64String(salt));
            _current = new Current(start, start.AddMonths(1), key);
            return key;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Test hooks reseed the database (and its salts); forget the cached one.</summary>
    public void Forget() => _current = null;
}

/// <summary>
/// Hash: a stable keyed hash for values never stored (rate-limit keys, cookie signatures), so limits and year-long
/// device cookies survive the monthly rotation. Stored: the rotating hash for IPs, devices and sessions in the database.
/// </summary>
public sealed class Hasher(byte[] secret, HashSalts salts) : IHasher
{
    public string Hash(string value) => Hex(secret, value);

    public string Stored(string value) => Hex(salts.CurrentKey(), value);

    private static string Hex(byte[] key, string value) => Convert.ToHexStringLower(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value)))[..32];
}
