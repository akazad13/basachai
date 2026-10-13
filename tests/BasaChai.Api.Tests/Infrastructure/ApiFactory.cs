using System.Text.Json;
using BasaChai.Application.Common;
using BasaChai.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace BasaChai.Api.Tests.Infrastructure;

/// <summary>The API on a real PostgreSQL 17 in a container, with a fake clock and a hand-written photo store (no Moq, D21).</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder().WithImage("postgres:17").Build();

    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-10-13T06:00:00Z"));
    public FakePhotoStore Photos { get; } = new();
    public FakePageCache Pages { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("ConnectionStrings:Main", _db.GetConnectionString());
        builder.UseSetting("Seed:Password", TestUsers.Password);
        builder.UseSetting("Auth:TestTotpBypass", "true");
        builder.UseSetting("Web:Origin", "http://localhost:3000");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IClockControl>();
            services.AddSingleton<IClockControl>(new FakeClockControl(Clock));
            services.RemoveAll<IPhotoStore>();
            services.AddSingleton<IPhotoStore>(Photos);
            services.RemoveAll<IPageCache>();
            services.AddSingleton<IPageCache>(Pages);
        });
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        await ResetAsync();
    }

    /// <summary>Wipes and reseeds; seed times are relative to the fake clock's current time.</summary>
    public async Task ResetAsync(string dataset = "pilot")
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TestSeeder>().SeedAsync(dataset, Clock.GetUtcNow());
    }

    /// <summary>A fresh context on the test database, for assertions.</summary>
    public AppDbContext Db() => new(Services.GetRequiredService<DbContextOptions<AppDbContext>>());

    public async Task<EventRow> LastEventAsync(string name)
    {
        await using var db = Db();
        var e = await db.AnalyticsEvents.Where(x => x.Name == name).OrderByDescending(x => x.CreatedAt).FirstAsync();
        return new EventRow(e.Name, e.Props.RootElement.Clone());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }

    private sealed class FakeClockControl(FakeTimeProvider clock) : IClockControl
    {
        public void Advance(TimeSpan by) => clock.Advance(by);
        public void Reset() { /* the fake clock only moves forward; tests seed relative to its current time */ }
    }
}

public sealed record EventRow(string Name, JsonElement Props);
