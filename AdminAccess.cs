using System.Net;

namespace GlurSolutions.Web;

/// <summary>
/// Decides whether the admin pages (/Admin/...) may be served.
/// This same app is deployed publicly to Azure, so the admin area must never answer there. It is served only
/// when BOTH are true: the app runs in the Development environment (your PC, via dotnet run or Visual Studio),
/// and the request comes from the same computer (loopback). Everywhere else /Admin is a plain 404.
/// </summary>
public static class AdminAccess
{
    public static bool Allowed(HttpContext context, IWebHostEnvironment env) =>
        env.IsDevelopment()
        && context.Connection.RemoteIpAddress is { } ip
        && IPAddress.IsLoopback(ip);
}
