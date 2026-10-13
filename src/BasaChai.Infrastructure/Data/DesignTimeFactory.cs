using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BasaChai.Infrastructure.Data;

/// <summary>Lets <c>dotnet ef migrations add</c> build the model without a running database.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("BASACHAI_DESIGN_DB") ?? "Host=localhost;Database=basachai_design")
            .UseSnakeCaseNamingConvention().Options);
}
