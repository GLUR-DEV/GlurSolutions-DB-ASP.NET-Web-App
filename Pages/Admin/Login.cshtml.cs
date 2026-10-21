using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GlurSolutions.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace GlurSolutions.Web.Pages.Admin;

public class LoginModel(IOptionsMonitor<AdminOptions> optionsMonitor, LoginThrottle throttle, ILogger<LoginModel> log) : PageModel
{
    public class InputModel
    {
        [Required(ErrorMessage = "Enter your username."), StringLength(100)]
        public string Username { get; set; } = "";

        [Required(ErrorMessage = "Enter your password."), StringLength(256)]
        public string Password { get; set; } = "";
    }

    [BindProperty] public InputModel Input { get; set; } = new();

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(SafeReturn(returnUrl));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        var options = optionsMonitor.CurrentValue;
        var client = AdminSecurity.ClientIp(HttpContext, options);

        if (throttle.IsLocked(client, out var wait))
        {
            log.LogWarning("Admin login blocked for {Client}: too many failed attempts.", client);
            ModelState.AddModelError(string.Empty,
                $"Too many failed attempts. Try again in {Math.Max(1, (int)Math.Ceiling(wait.TotalMinutes))} minute(s).");
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return Page();
        }

        if (!ModelState.IsValid) return Page();

        // Always do the full password work, whether or not the username matched,
        // so the response time does not reveal which one was wrong.
        var userOk = SameText(Input.Username, options.Username!);
        var passwordOk = userOk
            ? AdminPasswordHasher.Verify(Input.Password, options.PasswordHash)
            : AdminPasswordHasher.VerifyDummy(Input.Password);

        if (!(userOk && passwordOk))
        {
            throttle.RecordFailure(client);
            log.LogWarning("Failed admin login from {Client}.", client);   // never log the username or password typed
            ModelState.AddModelError(string.Empty, "Wrong username or password.");
            Input.Password = "";
            return Page();
        }

        throttle.Reset(client);

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, options.Username!),
             new Claim(AdminSecurity.StampClaim, AdminSecurity.StampFor(options.PasswordHash!))],
            CookieAuthenticationDefaults.AuthenticationScheme);

        var props = new AuthenticationProperties { IsPersistent = false };   // session cookie: gone when the browser closes
        props.Items[AdminSecurity.LoginAtKey] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), props);
        log.LogInformation("Admin signed in from {Client}.", client);
        return LocalRedirect(SafeReturn(returnUrl));
    }

    // Only ever go back to a page inside /Admin on this site (no open redirect).
    private string SafeReturn(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) &&
        returnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase)
            ? returnUrl
            : "/Admin";

    private static bool SameText(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(a)),
            SHA256.HashData(Encoding.UTF8.GetBytes(b)));
}
