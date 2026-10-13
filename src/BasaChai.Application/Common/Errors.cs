using BasaChai.Domain;

namespace BasaChai.Application.Common;

public sealed class RateLimitedException(TimeSpan retryAfter)
    : DomainException("rate_limited", "Too many requests. Try again later.", 429)
{
    public int RetryAfterSeconds { get; } = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
}

public sealed class NotFoundException(string what = "Not found.") : DomainException("not_found", what, 404);

public sealed class ForbiddenException(string code = "forbidden", string message = "Not allowed.") : DomainException(code, message, 403);

public static class Errors
{
    public static DomainException Conflict(string code, string message) => new(code, message, 409);
    public static DomainException BadRequest(string code, string message) => new(code, message, 400);
}
