using System.Security.Cryptography;
using System.Text;
using BasaChai.Application.Common;
using BasaChai.Application.Listings;
using BasaChai.Application.Moderation;
using BasaChai.Infrastructure.Data;
using BasaChai.Infrastructure.Services;
using CloudinaryDotNet;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BasaChai.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Test and Staging get the seed and clock hooks; Production never does.</summary>
    public static bool HasTestHooks(this IHostEnvironment env) => env.IsEnvironment("Test") || env.IsStaging();

    public static IServiceCollection AddBasaChai(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        var cs = config.GetConnectionString("Main") ?? throw new InvalidOperationException("ConnectionStrings:Main is not set.");
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs).UseSnakeCaseNamingConvention());
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        if (env.HasTestHooks())
        {
            var clock = new AdjustableClock();
            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton<IClockControl>(clock);
        }
        else services.AddSingleton(TimeProvider.System);

        var dp = services.AddDataProtection().SetApplicationName("BasaChai");
        if (config["DataProtection:KeyPath"] is { Length: > 0 } keyPath) dp.PersistKeysToFileSystem(new DirectoryInfo(keyPath));

        var secret = HashSalt(config, env);
        services.AddSingleton(sp => new HashSalts(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<TimeProvider>(), secret));
        services.AddSingleton<IHasher>(sp => new Hasher(secret, sp.GetRequiredService<HashSalts>()));
        services.AddSingleton<IRateLimiter, InMemoryRateLimiter>();
        services.AddSingleton<IAddressProtector, AddressProtector>();
        services.AddSingleton<ReferenceCache>();
        services.AddSingleton(new PhotoOptions($"basachai/{config["Cloudinary:Environment"] ?? env.EnvironmentName.ToLowerInvariant()}"));

        var cloud = config.GetSection("Cloudinary").Get<CloudinaryOptions>() ?? new CloudinaryOptions();
        if (cloud.CloudName.Length > 0 && cloud.ApiKey.Length > 0 && cloud.ApiSecret.Length > 0)
        {
            services.AddSingleton(cloud);
            services.AddSingleton(new Cloudinary(new Account(cloud.CloudName, cloud.ApiKey, cloud.ApiSecret)) { Api = { Secure = true } });
            services.AddSingleton<IPhotoStore, CloudinaryPhotoStore>();
        }
        else if (env.IsDevelopment() || env.IsEnvironment("Test"))
        {
            var local = new LocalPhotoStore(config["Photos:LocalPath"] ?? Path.Combine(Path.GetTempPath(), "basachai-photos"));
            services.AddSingleton(local);
            services.AddSingleton<IPhotoStore>(local);
        }
        else services.AddSingleton<IPhotoStore, UnconfiguredPhotoStore>();

        services.AddScoped<PublicListingService>();
        services.AddScoped<OwnerListingService>();
        services.AddScoped<SavedService>();
        services.AddScoped<ModerationService>();

        if (env.HasTestHooks())
        {
            services.AddSingleton(new SeedOptions
            {
                PilotFile = config["Seed:PilotFile"] ?? FindPilotFile(),
                Password = config["Seed:Password"] ?? Environment.GetEnvironmentVariable("E2E_PASSWORD") ?? "",
            });
            services.AddScoped<TestSeeder>();
        }
        return services;
    }

    /// <summary>
    /// Applies migrations and seeds reference data. With <paramref name="apiRole"/>, also grants that role what the API
    /// needs and no more: no schema changes, and <c>audit_log</c> is insert-only. Test hooks (Staging) truncate tables to
    /// reseed, so <paramref name="allowTruncate"/> lets them.
    /// </summary>
    public static async Task MigrateAsync(IServiceProvider services, string? apiRole = null, bool allowTruncate = false, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(ct);
        await ReferenceSeeder.SeedAsync(db, ct);
        if (string.IsNullOrEmpty(apiRole)) return;

        if (!System.Text.RegularExpressions.Regex.IsMatch(apiRole, "^[a-z_][a-z0-9_]{0,62}$"))
            throw new InvalidOperationException("Database:ApiRole must be a plain lower-case role name.");
        string[] grants =
        [
            $"GRANT USAGE ON SCHEMA public TO {apiRole}",
            $"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {apiRole}",
            $"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {apiRole}",
            $"REVOKE INSERT, UPDATE, DELETE ON \"__EFMigrationsHistory\" FROM {apiRole}",
            allowTruncate ? $"GRANT TRUNCATE ON ALL TABLES IN SCHEMA public TO {apiRole}"
                          : $"REVOKE UPDATE, DELETE, TRUNCATE ON audit_log FROM {apiRole}",
        ];
        // One transaction, so the running API never sees the brief grant on audit_log before its revoke.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var sql in grants) await db.Database.ExecuteSqlRawAsync(sql, ct);
        await tx.CommitAsync(ct);
    }

    private static byte[] HashSalt(IConfiguration config, IHostEnvironment env)
    {
        if (config["Hashing:Salt"] is { Length: >= 16 } salt) return Encoding.UTF8.GetBytes(salt);
        if (env.IsProduction()) throw new InvalidOperationException("Hashing:Salt must be set (16+ characters) in production.");
        return RandomNumberGenerator.GetBytes(32);   // fine for development and tests: hashes need only be stable per run
    }

    private static string FindPilotFile()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "deploy", "seed", "pilot.json");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("deploy/seed/pilot.json not found; set Seed:PilotFile.");
    }
}
