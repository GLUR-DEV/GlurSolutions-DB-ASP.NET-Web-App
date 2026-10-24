using System.Security.Cryptography;

namespace GlurSolutions.Web.Security;

/// <summary>
/// Password hashing for the admin login: PBKDF2 with SHA-256, a random salt per hash and 600,000 rounds (the current
/// OWASP guidance), so a stolen hash is very expensive to crack. Stored as: v1-pbkdf2-sha256.rounds.salt.hash
/// </summary>
public static class AdminPasswordHasher
{
    private const string Prefix = "v1-pbkdf2-sha256";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Rounds = 600_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Rounds, HashAlgorithmName.SHA256, KeySize);
        return $"{Prefix}.{Rounds}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    /// <summary>
    /// Checks a password against a stored hash. It does the same amount of work even when the stored value is
    /// missing or malformed, and compares in constant time, so response timing reveals nothing.
    /// </summary>
    public static bool Verify(string password, string? stored)
    {
        var salt = new byte[SaltSize];
        var expected = new byte[KeySize];
        var rounds = Rounds;
        var wellFormed = false;

        var parts = (stored ?? "").Split('.');
        if (parts.Length == 4 && parts[0] == Prefix && int.TryParse(parts[1], out var parsedRounds)
            && parsedRounds is >= 100_000 and <= 5_000_000)
        {
            try
            {
                var parsedSalt = Convert.FromBase64String(parts[2]);
                var parsedKey = Convert.FromBase64String(parts[3]);
                if (parsedSalt.Length >= 8 && parsedKey.Length == KeySize)
                {
                    salt = parsedSalt;
                    expected = parsedKey;
                    rounds = parsedRounds;
                    wellFormed = true;
                }
            }
            catch (FormatException)
            {
                wellFormed = false;
            }
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, rounds, HashAlgorithmName.SHA256, KeySize);
        var same = CryptographicOperations.FixedTimeEquals(actual, expected);
        return same && wellFormed;
    }
}
