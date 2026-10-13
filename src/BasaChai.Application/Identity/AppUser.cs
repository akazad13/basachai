using Microsoft.AspNetCore.Identity;

namespace BasaChai.Application.Identity;

/// <summary>Identity user. UserName holds the E.164 phone so Identity lookups work by number.</summary>
public class AppUser : IdentityUser<Guid>
{
    public string PhoneE164 { get; set; } = "";
    public DateTimeOffset? PhoneVerifiedAt { get; set; }
    public string DisplayName { get; set; } = "";
    /// <summary><c>active</c>, <c>restricted</c> (cannot post) or <c>suspended</c> (cannot sign in).</summary>
    public string Status { get; set; } = UserStatus.Active;
    public string PreferredLocale { get; set; } = "bn";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public class AppRole : IdentityRole<Guid>
{
    public AppRole() { }
    public AppRole(string name) : base(name) { }
}

public static class UserStatus
{
    public const string Active = "active";
    public const string Restricted = "restricted";
    public const string Suspended = "suspended";
}

public static class Roles
{
    public const string Member = "member";
    public const string Owner = "owner";
    public const string Moderator = "moderator";
    public const string Admin = "admin";
    public const string Agent = "agent";
    public static readonly string[] All = [Member, Owner, Moderator, Admin, Agent];
    public const string Staff = Moderator + "," + Admin;
}
