using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

internal class ElicitationRequest
{
    public string ElicitationId { get; set; } = string.Empty;
    public string ElicitationUrl { get; set; } = string.Empty;
    public TaskCompletionSource<Dictionary<string, JsonElement>> CompletionSource { get; set; } = new();
    public Dictionary<string, JsonElement>? UserData { get; set; }
}

internal class ElicitationTools
{
    // Static dictionary to hold pending elicitation requests by ID
    private static readonly ConcurrentDictionary<string, ElicitationRequest> PendingRequests = new();

    // Static method to remove and return a request
    public static bool TryRemoveRequest(string elicitationId, out ElicitationRequest? request)
    {
        return PendingRequests.TryRemove(elicitationId, out request);
    }

    // Static method to peek at a request without removing it
    public static ElicitationRequest? PeekRequest(string elicitationId)
    {
        PendingRequests.TryGetValue(elicitationId, out var request);
        return request;
    }

    [McpServerTool, Description("Trigger URL Mode Elicitation")]
    public async Task<string> TriggerUrlModeElicitation(
        McpServer server, // Get the McpServer from DI container
        IHttpContextAccessor httpContextAccessor, // Get HTTP context to build absolute URL
        CancellationToken cancellationToken
    )
    {
        // Check if the client supports URL mode elicitation
        if (server.ClientCapabilities?.Elicitation?.Url == null)
        {
            // fail the tool call
            throw new McpException("Client does not support URL mode elicitation");
        }

        // Create a V4 UUID for the elicitation ID
        var elicitationId = Guid.NewGuid().ToString();

        // Construct the elicitation URL as an absolute URL for this server
        var httpContext = httpContextAccessor.HttpContext;
        var request = httpContext!.Request;
        var elicitationUrl = $"{request.Scheme}://{request.Host}/elicitation-info?id={elicitationId}";

        // Create a request object and add it to the queue
        var elicitationRequest = new ElicitationRequest
        {
            ElicitationId = elicitationId,
            ElicitationUrl = elicitationUrl,
            CompletionSource = new TaskCompletionSource<Dictionary<string, JsonElement>>()
        };
        PendingRequests.TryAdd(elicitationId, elicitationRequest);

        var elicitResponse = await server.ElicitAsync(new ElicitRequestParams
        {
            Mode = "url",
            Message = "Please provide sensitive information at this URL:",
            Url = elicitationUrl,
            ElicitationId = elicitationId,
        }, cancellationToken);

        // Check if user accepted the elicitation
        if (elicitResponse.Action != "accept")
        {
            // Remove from queue or mark as cancelled
            elicitationRequest.CompletionSource.TrySetCanceled();
            return "Maybe next time!";
        }

        // Wait for the endpoint to signal completion with user data
        try
        {
            var userData = await elicitationRequest.CompletionSource.Task.WaitAsync(TimeSpan.FromMinutes(5), cancellationToken);

            // Process the user data
            var result = new StringBuilder();
            result.AppendLine("Thank you for providing the information!");
            result.AppendLine("Received data:");
            foreach (var kvp in userData)
            {
                result.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
            return result.ToString();
        }
        catch (TimeoutException)
        {
            return "Timeout waiting for user to provide information.";
        }
    }
}
