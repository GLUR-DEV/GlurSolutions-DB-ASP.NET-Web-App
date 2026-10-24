using System.Security.Cryptography;
using System.Text;

namespace GlurSolutions.Web.Security;

/// <summary>
/// Two helper commands you run on your own computer (they never touch the database or start the website):
///   dotnet run -- hash-password     asks for a password and prints the hash to store as Admin__PasswordHash
///   dotnet run -- generate-secret   prints a random secret for Admin__OriginSecret / ORIGIN_SECRET
/// </summary>
public static class AdminCli
{
    private const int MinPasswordLength = 14;

    /// <summary>Runs the command named in args, if there is one. Returns true when it did (so Program.cs should exit).</summary>
    public static bool TryRun(string[] args)
    {
        if (args.Length == 0) return false;

        switch (args[0])
        {
            case "hash-password":
                HashPassword();
                return true;
            case "generate-secret":
                Console.WriteLine(NewSecret());
                return true;
            default:
                return false;
        }
    }

    // 48 random bytes as letters, digits, "-" and "_" (safe to paste into Azure and Cloudflare)
    private static string NewSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static void HashPassword()
    {
        Console.WriteLine($"Choose a long, unique password (at least {MinPasswordLength} characters; a passphrase of 4+ random words is ideal).");
        var first = ReadHidden("Password: ");
        if (first.Length < MinPasswordLength)
        {
            Console.WriteLine($"Too short: use at least {MinPasswordLength} characters. Nothing was created.");
            return;
        }
        var second = ReadHidden("Type it again: ");
        if (!string.Equals(first, second, StringComparison.Ordinal))
        {
            Console.WriteLine("The two passwords did not match. Nothing was created.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Store this value as Admin__PasswordHash (Azure) or Admin:PasswordHash (user-secrets):");
        Console.WriteLine();
        Console.WriteLine(AdminPasswordHasher.Hash(first));
        Console.WriteLine();
        Console.WriteLine("Do not store the password itself anywhere in the app or in git.");
    }

    // Reads a line without showing what is typed
    private static string ReadHidden(string prompt)
    {
        Console.Write(prompt);
        if (Console.IsInputRedirected) return Console.ReadLine() ?? "";

        var text = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (text.Length > 0) text.Length--;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                text.Append(key.KeyChar);
            }
        }
        Console.WriteLine();
        return text.ToString();
    }
}
