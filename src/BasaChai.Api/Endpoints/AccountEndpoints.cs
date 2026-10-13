using System.Security.Cryptography;
using System.Text;
using BasaChai.Api.Http;
using BasaChai.Application.Common;
using BasaChai.Application.Identity;
using BasaChai.Application.Listings;
using BasaChai.Domain;
using BasaChai.Domain.Listings;
using BasaChai.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BasaChai.Api.Endpoints;

public sealed record RegisterBody(string? Name, string? Phone, string? Password);
public sealed record LoginBody(string? Phone, string? Password, string? Code);
public sealed record ChangePasswordBody(string? CurrentPassword, string? NewPassword);
public sealed record ResetConfirmBody(string? Token, string? NewPassword);
public sealed record MeBody(string? Name, string? Locale);
public sealed record TwoFactorBody(string? Code, string? Password);
public sealed record MeDto(Guid Id, string Name, string Phone, string Locale, IReadOnlyList<string> Roles, bool TwoFactorEnabled, string Status);

/// <summary>Sign-in by mobile number and password. Nothing is ever sent to the phone (D9); resets are admin-assisted (D10).</summary>
public static class AccountEndpoints
{
    public const int RegistrationsPerDay = 5;
    public const int FailedLoginLimit = 10;
    public static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);
    public const int LoginsPerIpPerWindow = 50;   // stops password spraying across many numbers from one address

    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password1", "12345678", "123456789", "1234567890", "qwertyui", "qwerty123", "11111111", "00000000",
        "iloveyou", "abcd1234", "abc12345", "bangladesh", "dhaka1234", "basachai", "password123", "admin123", "letmein1",
        "welcome1", "87654321", "asdfghjk", "zxcvbnm1", "trustno1", "football", "baseball", "sunshine", "princess",
    };

    public static void MapAccount(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/v1/auth");

        auth.MapPost("/register", async (RegisterBody body, HttpContext http, UserManager<AppUser> users,
            SignInManager<AppUser> signIn, IRateLimiter limiter, TimeProvider clock, IAppDbContext db) =>
        {
            var errors = new Dictionary<string, string[]>();
            var name = body.Name?.Trim() ?? "";
            if (name.Length is < 2 or > 60) errors["name"] = ["name_length"];
            var phone = Phone.ToE164(body.Phone);
            if (phone is null) errors["phone"] = ["phone_err"];
            if (body.Password is null || body.Password.Length < 8) errors["password"] = ["pass_err"];
            else if (CommonPasswords.Contains(body.Password)) errors["password"] = ["pass_common"];
            if (errors.Count > 0) throw new ValidationFailedException(errors);

            // The limit comes first so the phone_taken answer cannot be used to test numbers without limit.
            var ctx = http.Context();
            if (limiter.TryHit("register-day", "ip:" + ctx.IpKey, RegistrationsPerDay, TimeSpan.FromDays(1)) is { } wait)
                throw new RateLimitedException(wait);
            if (await users.Users.AnyAsync(u => u.PhoneE164 == phone))
                throw Errors.Conflict("phone_taken", "This number already has an account. Sign in instead.");

            var user = new AppUser { UserName = phone, PhoneE164 = phone!, DisplayName = ListingEditor.Plain(name), CreatedAt = clock.GetUtcNow(), LockoutEnabled = false };
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                var created = await users.CreateAsync(user, body.Password!);
                if (!created.Succeeded)
                {
                    if (created.Errors.Any(e => e.Code is "DuplicateUserName")) throw Errors.Conflict("phone_taken", "This number already has an account. Sign in instead.");
                    throw new ValidationFailedException(new Dictionary<string, string[]> { ["password"] = ["pass_err"] });
                }
                await users.AddToRoleAsync(user, Roles.Member);
                await tx.CommitAsync();
            }
            await signIn.SignInAsync(user, isPersistent: true);
            CsrfMiddleware.Issue(http, rotate: true);
            return Results.Created("/v1/me", await Me(users, user));
        });

        auth.MapPost("/login", async (LoginBody body, HttpContext http, UserManager<AppUser> users, SignInManager<AppUser> signIn,
            IRateLimiter limiter, IConfiguration config, IHostEnvironment env) =>
        {
            var phone = Phone.ToE164(body.Phone);
            if (phone is null || string.IsNullOrEmpty(body.Password)) throw ValidationFailedException.For("phone", "phone_err");

            // Each attempt is reserved before the slow password check, so parallel guesses cannot slip past the limit.
            // A successful sign-in refunds its reservation. After 10 failures the number is refused for 15 minutes.
            var ipKey = "ip:" + http.Context().IpKey;
            if (limiter.TryHit("login-ip", ipKey, LoginsPerIpPerWindow, LockoutWindow) is { } ipWait) throw new RateLimitedException(ipWait);
            if (limiter.TryHit("login-fail", phone, FailedLoginLimit, LockoutWindow) is { } locked) throw new RateLimitedException(locked);

            var user = await users.Users.FirstOrDefaultAsync(u => u.PhoneE164 == phone && u.DeletedAt == null);
            if (user is null || !await users.CheckPasswordAsync(user, body.Password))
                throw new DomainException("bad_credentials", "That number and password do not match. Try again or contact support.", 401);
            if (user.Status == UserStatus.Suspended) throw new ForbiddenException("suspended", "This account is suspended. Contact support.");

            if (user.TwoFactorEnabled && !(env.HasTestHooks() && config.GetValue<bool>("Auth:TestTotpBypass")))
            {
                if (string.IsNullOrWhiteSpace(body.Code))
                {
                    limiter.Refund("login-fail", phone);   // the password was right; only the code is missing
                    throw new DomainException("two_factor_code_required", "Enter the code from your authenticator app.", 401);
                }
                if (!await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, body.Code.Replace(" ", "")))
                    throw new DomainException("bad_code", "That code is not right. Try the current code.", 401);
            }
            limiter.Refund("login-fail", phone);
            await signIn.SignInAsync(user, isPersistent: true);
            CsrfMiddleware.Issue(http, rotate: true);
            return Results.Ok(await Me(users, user));
        });

        auth.MapPost("/logout", async (SignInManager<AppUser> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); });

        auth.MapGet("/csrf", (HttpContext http) => Results.Ok(new { token = CsrfMiddleware.Issue(http) }));

        // The web app asks this on every page load; signed out is a normal answer, not a 401.
        auth.MapGet("/session", async (HttpContext http, UserManager<AppUser> users) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            var user = http.User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(http.User) : null;
            return Results.Ok(new { me = user is null || user.DeletedAt is not null ? null : await Me(users, user) });
        });

        auth.MapPost("/password/change", async (ChangePasswordBody body, HttpContext http, UserManager<AppUser> users,
            SignInManager<AppUser> signIn, IAppDbContext db, TimeProvider clock) =>
        {
            var user = await users.GetUserAsync(http.User) ?? throw new DomainException("unauthenticated", "Sign in first.", 401);
            CheckNewPassword(body.NewPassword);
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                var result = await users.ChangePasswordAsync(user, body.CurrentPassword ?? "", body.NewPassword!);
                if (!result.Succeeded) throw ValidationFailedException.For("currentPassword", "bad_credentials");
                db.Audit(clock.GetUtcNow(), user.Id, "password.change", "user", user.Id.ToString());
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            // ChangePasswordAsync rotated the security stamp, which ends every other session; keep this one.
            await signIn.RefreshSignInAsync(user);
            return Results.NoContent();
        }).RequireAuthorization();

        // The reset page asks this on open, so a used or expired link says so before anyone types a password.
        auth.MapGet("/password/reset/check", async (string? token, IAppDbContext db, TimeProvider clock) =>
        {
            var hash = HashToken(token ?? "");
            var now = clock.GetUtcNow();
            return Results.Ok(new { valid = await db.PasswordResetTokens.AnyAsync(t => t.TokenHash == hash && t.ConsumedAt == null && t.ExpiresAt > now) });
        });

        auth.MapPost("/password/reset/confirm", async (ResetConfirmBody body, HttpContext http, UserManager<AppUser> users,
            SignInManager<AppUser> signIn, IAppDbContext db, TimeProvider clock) =>
        {
            CheckNewPassword(body.NewPassword);
            var now = clock.GetUtcNow();
            var hash = HashToken(body.Token ?? "");
            await using var tx = await db.Database.BeginTransactionAsync();
            // Claiming the token is one conditional UPDATE, so two parallel redemptions cannot both succeed.
            var claimed = await db.PasswordResetTokens
                .Where(t => t.TokenHash == hash && t.ConsumedAt == null && t.ExpiresAt > now)
                .ExecuteUpdateAsync(u => u.SetProperty(t => t.ConsumedAt, now));
            var user = claimed == 1
                ? await users.FindByIdAsync((await db.PasswordResetTokens.Where(t => t.TokenHash == hash).Select(t => t.UserId).FirstAsync()).ToString())
                : null;
            if (user is null || user.DeletedAt is not null) throw Errors.BadRequest("reset_used", "This link has expired or was already used.");

            user.PasswordHash = users.PasswordHasher.HashPassword(user, body.NewPassword!);
            await users.UpdateSecurityStampAsync(user);   // saves the new hash and ends every other session
            db.Audit(now, user.Id, "password_reset.redeem", "user", user.Id.ToString());
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            // The person who just proved they hold the link is signed in here; every other session has ended.
            if (user.Status != UserStatus.Suspended)
            {
                await signIn.SignInAsync(user, isPersistent: true);
                CsrfMiddleware.Issue(http, rotate: true);
            }
            return Results.NoContent();
        });

        var me = app.MapGroup("/v1/me").RequireAuthorization();

        me.MapGet("", async (HttpContext http, UserManager<AppUser> users)
            => Results.Ok(await Me(users, await users.GetUserAsync(http.User) ?? throw new DomainException("unauthenticated", "Sign in first.", 401))));

        me.MapPatch("", async (MeBody body, HttpContext http, UserManager<AppUser> users) =>
        {
            var user = await users.GetUserAsync(http.User) ?? throw new DomainException("unauthenticated", "Sign in first.", 401);
            if (body.Name is not null)
            {
                var name = body.Name.Trim();
                if (name.Length is < 2 or > 60) throw ValidationFailedException.For("name", "name_length");
                user.DisplayName = ListingEditor.Plain(name);
            }
            if (body.Locale is not null)
            {
                if (body.Locale is not ("bn" or "en")) throw ValidationFailedException.For("locale", "invalid");
                user.PreferredLocale = body.Locale;
            }
            await users.UpdateAsync(user);
            return Results.Ok(await Me(users, user));
        });

        me.MapDelete("", async (HttpContext http, UserManager<AppUser> users, SignInManager<AppUser> signIn, IAppDbContext db, TimeProvider clock) =>
        {
            var user = await users.GetUserAsync(http.User) ?? throw new DomainException("unauthenticated", "Sign in first.", 401);
            var now = clock.GetUtcNow();
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                // Every listing is archived (kept for audit), including one waiting for review, so it can never go live
                // under a deleted account. Personal data is removed.
                var listings = await db.Listings.Where(l => l.OwnerId == user.Id && l.Status != ListingStatus.Archived).ToListAsync();
                foreach (var l in listings) { l.WithdrawForDeletedOwner(now); db.FlushHistory(l); l.FullAddressEnc = null; }
                db.Saved.RemoveRange(await db.Saved.Where(s => s.UserId == user.Id).ToListAsync());
                db.Audit(now, user.Id, "user.delete", "user", user.Id.ToString());
                await db.SaveChangesAsync();

                var placeholder = "+d" + user.Id.ToString("N")[..12];   // fits phone_e164 (16) and frees the real number
                user.DisplayName = "Deleted user";
                user.PhoneE164 = placeholder; user.UserName = placeholder; user.NormalizedUserName = placeholder;
                user.DeletedAt = now; user.Status = UserStatus.Suspended;
                user.PasswordHash = null; user.TwoFactorEnabled = false;
                var updated = await users.UpdateAsync(user);
                if (!updated.Succeeded) throw new InvalidOperationException("Account deletion failed: " + string.Join(", ", updated.Errors.Select(e => e.Code)));
                await users.UpdateSecurityStampAsync(user);
                await tx.CommitAsync();
            }
            await signIn.SignOutAsync();
            return Results.NoContent();
        });

        me.MapGet("/two-factor", async (HttpContext http, UserManager<AppUser> users) =>
        {
            var user = await users.GetUserAsync(http.User) ?? throw new DomainException("unauthenticated", "Sign in first.", 401);
            // Once enrolled, the shared key is never shown again: a stolen session must not yield the second factor.
            if (user.TwoFactorEnabled) return Results.Ok(new { enabled = true, sharedKey = (string?)null, uri = (string?)null });
            var key = await users.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(key)) { await users.ResetAuthenticatorKeyAsync(user); key = await users.GetAuthenticatorKeyAsync(user); }
            var uri = $"otpauth://totp/BasaChai:{Uri.EscapeDataString(user.PhoneE164)}?secret={key}&issuer=BasaChai&digits=6";
            return Results.Ok(new { enabled = false, sharedKey = key, uri });
        });

        me.MapPost("/two-factor", async (TwoFactorBody body, HttpContext http, UserManager<AppUser> users, SignInManager<AppUser> signIn,
            IAppDbContext db, TimeProvider clock) =>
        {
            var user = await users.GetUserAsync(http.User) ?? throw new DomainException("unauthenticated", "Sign in first.", 401);
            if (user.TwoFactorEnabled) throw Errors.Conflict("already_enabled", "Two-factor sign-in is already on.");
            // Re-authenticate: holding a session alone must not be enough to attach a new authenticator.
            if (!await users.CheckPasswordAsync(user, body.Password ?? "")) throw ValidationFailedException.For("password", "bad_credentials");
            var code = (body.Code ?? "").Replace(" ", "");
            if (!await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code))
                throw ValidationFailedException.For("code", "bad_code");
            await users.SetTwoFactorEnabledAsync(user, true);
            db.Audit(clock.GetUtcNow(), user.Id, "two_factor.enable", "user", user.Id.ToString());
            await db.SaveChangesAsync();
            await signIn.RefreshSignInAsync(user);
            return Results.NoContent();
        });

        me.MapGet("/attention", async (HttpContext http, OwnerListingService owner)
            => Results.Ok(new { count = await owner.AttentionCountAsync(http.User.RequiredUserId()) }));

        me.MapGet("/saved", async (HttpContext http, SavedService saved) => Results.Ok(await saved.ListAsync(http.User.RequiredUserId())));
        me.MapGet("/saved/ids", async (HttpContext http, SavedService saved) => Results.Ok(await saved.SavedIdsAsync(http.User.RequiredUserId())));
        me.MapPut("/saved/{key}", async (string key, HttpContext http, SavedService saved) =>
        {
            await saved.SaveAsync(http.User.RequiredUserId(), key);
            return Results.NoContent();
        });
        me.MapDelete("/saved/{key}", async (string key, HttpContext http, SavedService saved) =>
        {
            await saved.UnsaveAsync(http.User.RequiredUserId(), key);
            return Results.NoContent();
        });
    }

    public static async Task<MeDto> Me(UserManager<AppUser> users, AppUser user)
        => new(user.Id, user.DisplayName, user.PhoneE164, user.PreferredLocale, (await users.GetRolesAsync(user)).Order().ToList(),
            user.TwoFactorEnabled, user.Status);

    private static void CheckNewPassword(string? password)
    {
        if (password is null || password.Length < 8) throw ValidationFailedException.For("newPassword", "pass_err");
        if (CommonPasswords.Contains(password)) throw ValidationFailedException.For("newPassword", "pass_common");
    }

    public static string HashToken(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
