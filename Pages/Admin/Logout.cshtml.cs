using GlurSolutions.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlurSolutions.Web.Pages.Admin;

public class LogoutModel : PageModel
{
    // Signing out is a POST (with the antiforgery token), so another site cannot sign you out with a link or image
    public IActionResult OnGet() => RedirectToPage("Index");

    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(AdminAuth.Scheme);
        return RedirectToPage("Login");
    }
}
