using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ProtectedMcpServer.Tools;

public class ContextTools(IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool(UseStructuredContent = true)]
    [Description("Retrieves the user info (scopes) from the current HTTP context and returns them as a JSON object.")]
    public object GetUserInfo()
    {
        var context = httpContextAccessor.HttpContext;
        if (context == null)
        {
            return new { error = "No HTTP context available" };
        }

        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return new { error = "User is not authenticated" };
        }

        // Try to get scopes from various possible claim types
        var scopes = user.Claims
            .Where(claim => claim.Type == "scope")
            .SelectMany(claim => claim.Value.Split(' '))
            .ToList();

        var userName = user.Identity.Name ?? "Unknown";
        var email = user.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value;

        return new
        {
            user = userName,
            email = email,
            scopes = scopes
        };
    }
}
