using BasaChai.Api.Endpoints;
using BasaChai.Api.Http;
using BasaChai.Application.Identity;
using BasaChai.Infrastructure;
using BasaChai.Infrastructure.Data;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var env = builder.Environment;

// `migrate`: apply migrations and reference data with the migration role, grant the API role its rights, then exit.
// Production runs this as a separate step before starting a new image (security-and-operations spec, Deploy).
var migrateOnly = args.FirstOrDefault() == "migrate";
if (migrateOnly && config.GetConnectionString("Migrations") is { Length: > 0 } migrations)
    config["ConnectionStrings:Main"] = migrations;

// Structured JSON logs for Docker everywhere but local development and the test runs.
if (!env.IsDevelopment() && !env.IsEnvironment("Test"))
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(o => { o.UseUtcTimestamp = true; o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ"; });
}

const long MaxBody = 12 * 1024 * 1024;   // one 10 MB photo plus multipart overhead (architecture spec: 12 MB)
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = MaxBody);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = MaxBody);

builder.Services.AddBasaChai(config, env);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services.AddIdentityCore<AppUser>(o =>
    {
        o.Password.RequiredLength = 8;
        o.Password.RequireDigit = o.Password.RequireLowercase = o.Password.RequireUppercase = o.Password.RequireNonAlphanumeric = false;
        o.User.AllowedUserNameCharacters = "+0123456789-abcdefghijklmnopqrstuvwxyz";
        o.Lockout.AllowedForNewUsers = false;   // lockout is the API's own 10-failures rule, timed by TimeProvider
    })
    .AddRoles<AppRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = CsrfMiddleware.AuthCookie;
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    // Secure everywhere except plain-HTTP local and test runs (staging included).
    o.Cookie.SecurePolicy = env.IsDevelopment() || env.IsEnvironment("Test") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    if (config["Auth:CookieDomain"] is { Length: > 0 } domain) o.Cookie.Domain = domain;
    o.ExpireTimeSpan = TimeSpan.FromDays(30);
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c => Problem(c.HttpContext, 401, "unauthenticated", "Sign in first.");
    o.Events.OnRedirectToAccessDenied = c => Problem(c.HttpContext, 403, "forbidden", "Not allowed.");
});
// Check the security stamp on every request so a password change, reset or suspension ends other sessions at once.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AdminEndpoints.StaffPolicy, p => p.RequireRole(Roles.Moderator, Roles.Admin))
    .AddPolicy(AdminEndpoints.AdminPolicy, p => p.RequireRole(Roles.Admin));

// Behind Caddy the API sees plain HTTP; trust X-Forwarded-Proto/For only from the configured proxy address.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
    foreach (var proxy in config.GetSection("Network:KnownProxies").Get<string[]>() ?? [])
        o.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(config["Web:Origin"] ?? "http://localhost:3000")
    .AllowCredentials()
    .WithHeaders("Content-Type", "If-Match", CsrfMiddleware.Header, "X-Search-Source", "X-Session-Id")
    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")));

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>(tags: ["ready"]);

var app = builder.Build();

// `cloudinary-setup`: configure the Cloudinary account (upload preset and named sizes), then exit.
if (args.FirstOrDefault() == "cloudinary-setup")
{
    var cloudinary = app.Services.GetService<CloudinaryDotNet.Cloudinary>()
        ?? throw new InvalidOperationException("Cloudinary is not configured: set Cloudinary:CloudName, ApiKey and ApiSecret.");
    foreach (var line in await BasaChai.Infrastructure.Services.CloudinarySetup.RunAsync(
                 cloudinary, app.Services.GetRequiredService<BasaChai.Infrastructure.Services.CloudinaryOptions>()))
        Console.WriteLine(line);
    return;
}

if (migrateOnly)
{
    await DependencyInjection.MigrateAsync(app.Services, apiRole: config["Database:ApiRole"], allowTruncate: env.HasTestHooks());
    return;
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
// This month's salt for stored IP and device hashes; a database read only on the first request of a month.
// Health checks skip it, so liveness does not depend on the database.
app.Use(async (http, next) =>
{
    if (!http.Request.Path.StartsWithSegments("/health"))
        // Not the request's token: one visitor closing the tab must not fail the month's salt load (or log a 500).
        await http.RequestServices.GetRequiredService<BasaChai.Infrastructure.Services.HashSalts>().EnsureAsync(CancellationToken.None);
    await next(http);
});
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CsrfMiddleware>();

if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") });
app.MapHealthChecks("/health", new() { Predicate = c => c.Tags.Contains("ready") });

app.MapListings();
app.MapAccount();
app.MapAdmin();
if (env.HasTestHooks())
{
    if (env.IsStaging() && string.IsNullOrEmpty(config["Test:HookKey"]))
        throw new InvalidOperationException("Staging exposes test hooks only behind Test:HookKey; set it.");
    app.MapTestHooks();
}

// Production applies migrations with the EF migration bundle and a separate migration role (security-and-operations spec).
if (config.GetValue("Database:MigrateOnStart", !env.IsProduction())) await DependencyInjection.MigrateAsync(app.Services);

app.Run();

static Task Problem(HttpContext http, int status, string code, string title)
{
    http.Response.StatusCode = status;
    return http.Response.WriteAsJsonAsync(new { type = $"https://basachai.app/errors/{code}", title, status, code },
        options: null, contentType: "application/problem+json");
}

public partial class Program;
