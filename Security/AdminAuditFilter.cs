using System.Globalization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GlurSolutions.Web.Security;

/// <summary>
/// Writes one log line for every change made in the admin (every POST except the login itself): who, from where,
/// which page and action, which record numbers, and whether it worked. It never logs form text or passwords.
/// Read these under Monitoring > Log stream on Azure.
/// </summary>
public sealed class AdminAuditFilter : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var executed = await next();

        var http = context.HttpContext;
        if (!HttpMethods.IsPost(http.Request.Method)) return;

        var page = context.ActionDescriptor.ViewEnginePath;
        if (page.EndsWith("/Login", StringComparison.OrdinalIgnoreCase)) return;   // the login logs itself

        // Only record numbers (id, projectId, slideshowId ...), never text the visitor typed
        var records = string.Join(", ", context.HandlerArguments
            .Where(a => a.Key.EndsWith("id", StringComparison.OrdinalIgnoreCase) && a.Value is int)
            .Select(a => $"{a.Key}={Convert.ToString(a.Value, CultureInfo.InvariantCulture)}"));

        var logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AdminAudit");
        logger.LogInformation(
            "Admin change: user={User} ip={Ip} page={Page} action={Action} {Records} result={Result}",
            http.User.Identity?.Name ?? "anonymous",
            http.Items[AdminGateMiddleware.ClientIpItem] as string ?? "unknown",
            page,
            context.HandlerMethod?.Name ?? "Post",
            records,
            executed.Exception is null ? "ok" : "failed");
    }
}
