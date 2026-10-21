using System.Security.Cryptography;

namespace GlurSolutions.Web.Security;

/// <summary>
/// PBKDF2-SHA256 password hashing. Stored format: v1.{iterations}.{saltBase64}.{hashBase64}
/// The version and iteration count are inside the string, so the cost can be raised later
/// without breaking existing hashes.
/// </summary>
public static class AdminPasswordHasher
{
    private const string Version = "v1";
    private const int Iterations = 600_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int MinAcceptedIterations = 100_000;
    private const int MaxAcceptedIterations = 5_000_000;

    // A real hash of a throwaway value. Verified against when the username is wrong,
    // so a wrong username and a wrong password take the same time.
    private static readonly Lazy<string> DummyHash = new(() => Hash(Guid.NewGuid().ToString("N")));

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Version}.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    /// <summary>True when the password matches the stored hash. Malformed hashes simply return false.</summary>
    public static bool Verify(string password, string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return false;

        var parts = stored.Split('.');
        if (parts.Length != 4 || parts[0] != Version) return false;
        if (!int.TryParse(parts[1], out var iterations) ||
            iterations < MinAcceptedIterations || iterations > MaxAcceptedIterations) return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }
        if (salt.Length < 8 || expected.Length < 16) return false;

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>Burns the same effort as a real check. Always returns false.</summary>
    public static bool VerifyDummy(string password)
    {
        Verify(password, DummyHash.Value);
        return false;
    }
}
