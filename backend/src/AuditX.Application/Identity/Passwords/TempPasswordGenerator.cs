using System.Security.Cryptography;
using AuditX.Domain.Identity;

namespace AuditX.Application.Identity.Passwords;

/// <summary>
/// Generates a strong temporary password that satisfies the institution's complexity policy. Always includes
/// one of each required character class and fills the remaining length from a mixed alphabet, then shuffles —
/// so the result passes <see cref="PasswordPolicyEvaluator"/> for any enabled policy.
/// </summary>
public static class TempPasswordGenerator
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";   // no I/O
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";   // no l/o
    private const string Digits = "23456789";                  // no 0/1
    private const string Symbols = "!@#$%^&*?-_+=";

    public static string Generate(InstitutionSettings settings)
    {
        // Comfortably above the policy minimum, capped so it stays typeable.
        var length = Math.Clamp(Math.Max(settings.PasswordMinLength, 16), 8, 64);

        var required = new List<char>();
        var alphabet = string.Empty;

        void Include(bool enabled, string set)
        {
            alphabet += set;
            if (enabled)
            {
                required.Add(Pick(set));
            }
        }

        // Always draw from every class so the result is strong even when a class is not strictly required.
        Include(settings.PasswordRequireUppercase, Upper);
        Include(settings.PasswordRequireLowercase, Lower);
        Include(settings.PasswordRequireDigit, Digits);
        Include(settings.PasswordRequireSymbol, Symbols);

        var chars = new List<char>(required);
        while (chars.Count < length)
        {
            chars.Add(Pick(alphabet));
        }

        Shuffle(chars);
        return new string(chars.ToArray());
    }

    private static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];

    private static void Shuffle(IList<char> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
