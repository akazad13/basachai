using System.Net;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using BasaChai.Application.Common;
using BasaChai.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BasaChai.Api.Http;

/// <summary>Maps rule violations to RFC 9457 ProblemDetails with a stable <c>code</c> the web app translates.</summary>
public sealed class ProblemExceptionHandler(IProblemDetailsService problems, ILogger<ProblemExceptionHandler> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, code, title) = exception switch
        {
            DomainException d => (d.Status, d.Code, d.Message),
            BadHttpRequestException b => (b.StatusCode, b.StatusCode == 413 ? "photo_too_large" : "bad_request", "The request could not be read."),
            _ => (500, "server_error", "Something went wrong. Try again."),
        };
        if (status >= 500) log.LogError(exception, "Unhandled error on {Method} {Path}", http.Request.Method, http.Request.Path);

        var problem = new ProblemDetails { Status = status, Title = title, Type = $"https://basachai.app/errors/{code}" };
        problem.Extensions["code"] = code;
        if (exception is ValidationFailedException v) problem.Extensions["errors"] = v.Errors;
        if (exception is RateLimitedException r)
        {
            problem.Extensions["retryAfter"] = r.RetryAfterSeconds;
            http.Response.Headers.RetryAfter = r.RetryAfterSeconds.ToString();
        }
        http.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem, Exception = exception });
    }
}

public static class RequestContexts
{
    public const string DeviceCookie = "bc_dev";
    public const string SessionCookie = "bc_sid";

    /// <summary>
    /// Who is calling. The IP comes from CF-Connecting-IP, trusted because the firewall admits only Cloudflare.
    /// Device and session cookies are issued only when <paramref name="issueCookies"/> is set: cacheable GETs must
    /// never carry Set-Cookie, or a shared cache would hand one visitor's cookie to everyone.
    /// </summary>
    public static RequestContext Context(this HttpContext http, bool issueCookies = false)
    {
        var hasher = http.RequestServices.GetRequiredService<IHasher>();
        var device = ReadDevice(http, hasher) ?? (issueCookies ? IssueDevice(http, hasher) : "none");
        var session = http.Request.Headers["X-Session-Id"].FirstOrDefault() is { Length: > 0 and <= 64 } s ? s
            : http.Request.Cookies.TryGetValue(SessionCookie, out var c) && c.Length is > 10 and < 64 ? c
            : issueCookies ? Issue(http, SessionCookie, NewId(), null) : null;
        var roles = http.User.FindAll(ClaimTypes.Role).Select(r => r.Value).ToArray();
        var ip = "ip:" + ClientNetwork(http);
        return new RequestContext(http.User.UserId(), hasher.Stored(ip), hasher.Stored("device:" + device),
            session is null ? null : hasher.Stored("session:" + session), roles)
        {
            IpKey = hasher.Hash(ip), DeviceKey = hasher.Hash("device:" + device), SessionKey = session is null ? null : hasher.Hash("session:" + session),
        };
    }

    public static Guid? UserId(this ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static Guid RequiredUserId(this ClaimsPrincipal user)
        => user.UserId() ?? throw new DomainException("unauthenticated", "Sign in first.", 401);

    /// <summary>The client address; IPv6 is grouped by /64 because one subscriber usually holds a whole /64.</summary>
    public static string ClientNetwork(HttpContext http)
    {
        var raw = http.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? http.Connection.RemoteIpAddress?.ToString();
        if (raw is null || !IPAddress.TryParse(raw, out var ip)) return "unknown";
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6) return ip.ToString();
        var bytes = ip.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }

    /// <summary>Whether cookies must be Secure: always, except plain-HTTP local and test runs.</summary>
    public static bool SecureCookies(HttpContext http)
        => http.Request.IsHttps || !(http.RequestServices.GetRequiredService<IHostEnvironment>() is var env && (env.IsDevelopment() || env.IsEnvironment("Test")));

    public static string? CookieDomain(HttpContext http)
        => http.RequestServices.GetRequiredService<IConfiguration>()["Auth:CookieDomain"] is { Length: > 0 } d ? d : null;

    // The device id is signed, so a client cannot pick ids to spread reveals across many "devices" cheaply.
    private static string? ReadDevice(HttpContext http, IHasher hasher)
    {
        if (!http.Request.Cookies.TryGetValue(DeviceCookie, out var v)) return null;
        var dot = v.LastIndexOf('.');
        if (dot <= 0) return null;
        var (id, sig) = (v[..dot], v[(dot + 1)..]);
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(sig), Encoding.UTF8.GetBytes(hasher.Hash("device-sig:" + id)[..16])) ? id : null;
    }

    private static string IssueDevice(HttpContext http, IHasher hasher)
    {
        var id = NewId();
        Issue(http, DeviceCookie, $"{id}.{hasher.Hash("device-sig:" + id)[..16]}", TimeSpan.FromDays(365));
        return id;
    }

    private static string NewId() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)).Replace('+', '-').Replace('/', '_');

    private static string Issue(HttpContext http, string name, string value, TimeSpan? maxAge)
    {
        http.Response.Cookies.Append(name, value, new CookieOptions
        {
            HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = SecureCookies(http), MaxAge = maxAge, IsEssential = true, Domain = CookieDomain(http),
        });
        return value;
    }
}

/// <summary>
/// Double-submit antiforgery: a signed-in browser must echo the XSRF-TOKEN cookie in the X-XSRF-TOKEN header
/// on every state-changing request. Requests without a session cookie have nothing to forge.
/// </summary>
public sealed class CsrfMiddleware(RequestDelegate next)
{
    public const string Cookie = "XSRF-TOKEN";
    public const string Header = "X-XSRF-TOKEN";
    public const string AuthCookie = "bc_auth";

    public async Task InvokeAsync(HttpContext http)
    {
        var method = http.Request.Method;
        var unsafeMethod = !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method));
        var exempt = http.Request.Path.StartsWithSegments("/v1/auth/login") || http.Request.Path.StartsWithSegments("/v1/auth/register")
                     || http.Request.Path.StartsWithSegments("/v1/test");
        if (unsafeMethod && !exempt && http.Request.Cookies.ContainsKey(AuthCookie))
        {
            var cookie = http.Request.Cookies[Cookie];
            var header = http.Request.Headers[Header].FirstOrDefault();
            if (string.IsNullOrEmpty(cookie) || header is null
                || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(cookie), Encoding.UTF8.GetBytes(header)))
                throw new ForbiddenException("csrf_failed", "Refresh the page and try again.");
        }
        await next(http);
    }

    /// <summary>Issues (or returns the existing) token, readable by the web app's script.</summary>
    public static string Issue(HttpContext http, bool rotate = false)
    {
        if (!rotate && http.Request.Cookies.TryGetValue(Cookie, out var existing) && existing.Length > 20) return existing;
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        http.Response.Cookies.Append(Cookie, token, new CookieOptions
        {
            HttpOnly = false, SameSite = SameSiteMode.Lax, Secure = RequestContexts.SecureCookies(http), IsEssential = true, Path = "/",
            Domain = RequestContexts.CookieDomain(http),
        });
        return token;
    }
}
