using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace ProtectedMcpServer.Middleware;

/// <summary>
/// Enforces required OAuth scopes for protected MCP tool calls before handing off to the MCP endpoint.
/// </summary>
public sealed class CheckScopesMiddleware(RequestDelegate next, ILogger<CheckScopesMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method == HttpMethods.Post && context.Request.Path == "/")
        {
            context.Request.EnableBuffering();

            var message = await JsonSerializer.DeserializeAsync(
                context.Request.Body,
                McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(JsonRpcMessage)),
                context.RequestAborted) as JsonRpcMessage;

            context.Request.Body.Position = 0;

            if (message is JsonRpcRequest request && request.Method == "tools/call")
            {
                var toolCallParams = JsonSerializer.Deserialize(
                    request.Params,
                    McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(CallToolRequestParams))) as CallToolRequestParams;

                if (toolCallParams?.Name == "make_it_rain")
                {
                    var user = context.User;
                    var userScopes = user.Claims
                        .Where(c => c.Type == "scope" || c.Type == "scp")
                        .SelectMany(c => c.Value.Split(' '))
                        .Distinct()
                        .ToList();

                    if (!userScopes.Contains("rain:god"))
                    {
                        logger.LogWarning(
                            "Scope check failed for tool call {ToolName}. Missing required scope {RequiredScope}. User: {UserName}",
                            toolCallParams.Name,
                            "rain:god",
                            context.User.Identity?.Name ?? "unknown");

                        userScopes.Add("rain:god");
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.Headers.WWWAuthenticate =
                            $"Bearer error=\"insufficient_scope\", scope=\"{string.Join(" ", userScopes)}\"";
                        await context.Response.StartAsync(context.RequestAborted);
                        await context.Response.Body.FlushAsync(context.RequestAborted);
                        return;
                    }
                }
            }
        }

        await next(context);
    }
}
