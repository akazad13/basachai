using System.Text;
using System.Text.RegularExpressions;

namespace BasaChai.Application.Common;

/// <summary>Bangladeshi mobile numbers: accepts 01X, 8801X or +8801X, spaces or dashes, Bangla or Latin digits.</summary>
public static partial class Phone
{
    [GeneratedRegex(@"^(?:\+?880|0)1[3-9]\d{8}$")]
    private static partial Regex Valid();

    /// <summary>Returns <c>+8801XXXXXXXXX</c>, or null when the input is not a valid Bangladeshi mobile number.</summary>
    public static string? ToE164(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var sb = new StringBuilder(input.Length);
        foreach (var c in input.Trim())
        {
            if (c is >= '০' and <= '৯') sb.Append((char)('0' + (c - '০')));
            else if (c is ' ' or '-' ) continue;
            else sb.Append(c);
        }
        var s = sb.ToString();
        if (!Valid().IsMatch(s)) return null;
        return "+880" + s[^10..];
    }

    /// <summary>Every format a number might appear in, for leak checks: E.164, local, dashed, Bangla digits.</summary>
    public static IEnumerable<string> AllFormats(string e164)
    {
        var local = "0" + e164[^10..];
        yield return e164;
        yield return local;
        yield return local[..5] + "-" + local[5..];
        yield return ToBangla(local);
    }

    public static string ToBangla(string latin)
        => string.Concat(latin.Select(c => c is >= '0' and <= '9' ? (char)('০' + (c - '0')) : c));
}
