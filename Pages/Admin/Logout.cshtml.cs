using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlurSolutions.Web.Pages.Admin;

public class LogoutModel : PageModel
{
    // A plain link must not sign anyone out (that would be a forgeable action), so GET just goes back.
    public IActionResult OnGet() => RedirectToPage("/Admin/Index");

    // POST only; the form carries an antiforgery token.
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Admin/Login");
    }
}
