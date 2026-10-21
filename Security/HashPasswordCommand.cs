using System.Text;

namespace GlurSolutions.Web.Security;

/// <summary>dotnet run -- hash-password : prints a hash to paste into Admin__PasswordHash. Nothing is stored.</summary>
public static class HashPasswordCommand
{
    private const int MinLength = 14;

    public static void Run()
    {
        Console.WriteLine("Create the admin password hash. The password itself is never saved.");
        Console.WriteLine($"Use a long random passphrase (at least {MinLength} characters), ideally from a password manager.");
        Console.WriteLine();

        var first = ReadSecret("Password: ");
        if (first.Length < MinLength)
        {
            Console.WriteLine($"Too short: use at least {MinLength} characters.");
            return;
        }
        var second = ReadSecret("Repeat it: ");
        if (first != second)
        {
            Console.WriteLine("The two entries do not match. Nothing was created.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Paste this into the Admin__PasswordHash setting (Azure) or user-secrets (local):");
        Console.WriteLine();
        Console.WriteLine(AdminPasswordHasher.Hash(first));
        Console.WriteLine();
        Console.WriteLine("Local:  dotnet user-secrets set \"Admin:PasswordHash\" \"<the hash>\"");
    }

    private static string ReadSecret(string prompt)
    {
        Console.Write(prompt);
        if (Console.IsInputRedirected)
            return Console.ReadLine() ?? "";

        var sb = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace) { if (sb.Length > 0) sb.Length--; }
            else if (!char.IsControl(key.KeyChar)) sb.Append(key.KeyChar);
        }
        Console.WriteLine();
        return sb.ToString();
    }
}
