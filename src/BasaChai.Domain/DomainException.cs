namespace BasaChai.Domain;

/// <summary>A rule violation with a stable error code the API returns in ProblemDetails.</summary>
public class DomainException(string code, string message, int status = 409) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public sealed class InvalidTransitionException(string from, string action)
    : DomainException("invalid_transition", $"'{action}' is not allowed from '{from}'.");

public sealed class PhotosRequiredException(int have)
    : DomainException("photos_required", $"At least 3 photos are needed to submit; {have} stored.");

public sealed class ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
    : DomainException("validation_failed", "Some fields are missing or invalid.", 400)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public static ValidationFailedException For(string field, string messageKey = "required")
        => new(new Dictionary<string, string[]> { [field] = [messageKey] });
}
