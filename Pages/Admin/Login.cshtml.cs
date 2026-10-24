using System.Security.Cryptography;
using System.Text;
using GlurSolutions.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace GlurSolutions.Web.Pages.Admin;

[AllowAnonymous]
public class LoginModel(IOptions<AdminSettings> options, LoginThrottle throttle, ILogger<LoginModel> logger) : PageModel
{
    [BindProperty] public string? Username { get; set; }
    [BindProperty] public string? Password { get; set; }

    public string? Error { get; private set; }

    public IActionResult OnGet() =>
        User.Identity?.IsAuthenticated == true ? RedirectToPage("Index") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        var settings = options.Value;
        var ip = HttpContext.Items[AdminGateMiddleware.ClientIpItem] as string ?? "unknown";

        // Blocked visitors are turned away before any password work is done
        if (throttle.IsLockedOut(ip, out var retryAfter))
        {
            logger.LogWarning("Admin login blocked after repeated failures from {Ip}", ip);
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            Error = $"Too many failed attempts. Try again in {Math.Ceiling(retryAfter.TotalMinutes)} minute(s).";
            return Page();
        }

        var username = (Username ?? "").Trim();
        var password = Password ?? "";
        if (password.Length > 256) password = password[..256];   // no real password is this long; stops giant inputs

        // Both checks always run, so timing does not reveal whether the user name or the password was wrong
        var userOk = CryptographicOperations.FixedTimeEquals(Sha(username), Sha(settings.Username ?? ""));
        var passwordOk = AdminPasswordHasher.Verify(password, settings.PasswordHash);

        if (!(userOk && passwordOk))
        {
            throttle.RecordFailure(ip);
            logger.LogWarning("Failed admin login from {Ip}", ip);       // the attempted user name is deliberately not logged
            await Task.Delay(750);                                      // makes every guess slower
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            Error = "Wrong username or password.";                      // the same message whatever was wrong
            return Page();
        }

        throttle.Reset(ip);
        await HttpContext.SignInAsync(
            AdminAuth.Scheme,
            AdminAuth.CreatePrincipal(settings.Username!, settings.PasswordHash!),
            new AuthenticationProperties { IsPersistent = false });     // a session cookie: gone when the browser closes
        logger.LogInformation("Admin signed in from {Ip}", ip);
        return RedirectToPage("Index");                                 // always the same fixed place: no redirect parameter to abuse
    }

    private static byte[] Sha(string text) => SHA256.HashData(Encoding.UTF8.GetBytes(text));
}
