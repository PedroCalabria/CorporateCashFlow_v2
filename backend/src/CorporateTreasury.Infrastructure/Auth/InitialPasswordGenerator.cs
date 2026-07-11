using System.Security.Cryptography;
using CorporateTreasury.Application.Interfaces;

namespace CorporateTreasury.Infrastructure.Auth;

/// <summary>
/// Generates a strong random initial password using a cryptographic RNG. The result always
/// contains at least one lowercase, one uppercase, one digit, and one symbol so it satisfies the
/// reset-password strength rule too, and the character order is shuffled without modulo bias.
/// </summary>
public sealed class InitialPasswordGenerator : IInitialPasswordGenerator
{
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%*?";
    private const string All = Lower + Upper + Digits + Symbols;
    private const int Length = 16;

    public string Generate()
    {
        var chars = new char[Length];

        // Guarantee one of each class, then fill the rest from the full alphabet.
        chars[0] = Pick(Lower);
        chars[1] = Pick(Upper);
        chars[2] = Pick(Digits);
        chars[3] = Pick(Symbols);
        for (var i = 4; i < Length; i++)
        {
            chars[i] = Pick(All);
        }

        Shuffle(chars);
        return new string(chars);
    }

    private static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

    private static void Shuffle(char[] chars)
    {
        // Fisher–Yates with an unbiased RNG.
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
    }
}
