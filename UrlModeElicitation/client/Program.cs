using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

var endpoint = Environment.GetEnvironmentVariable("ENDPOINT") ?? "http://localhost:3001";

var clientTransport = new HttpClientTransport(new()
{
    Endpoint = new Uri(endpoint),
    TransportMode = HttpTransportMode.StreamableHttp,
});

McpClient mcpClient = null!;

McpClientOptions options = new()
{
    ClientInfo = new()
    {
        Name = "ElicitationClient",
        Version = "1.0.0"
    },
    ProtocolVersion = "2025-11-25",
    Capabilities = new()
    {
        Elicitation = new()
        {
            Url = new()
        }
    },
    Handlers = new()
    {
        ElicitationHandler = HandleElicitationAsync,
    }
};

mcpClient = await McpClient.CreateAsync(clientTransport, options);

var tools = await mcpClient.ListToolsAsync();
foreach (var tool in tools)
{
    Console.WriteLine($"Connected to server with tools: {tool.Name}");
}

var toolName = "trigger_url_mode_elicitation";

Console.WriteLine($"Calling tool: {toolName}");

var result = await mcpClient.CallToolAsync(toolName: toolName);

foreach (var block in result.Content)
{
    if (block is TextContentBlock textBlock)
    {
        Console.WriteLine(textBlock.Text);
    }
    else
    {
        Console.WriteLine($"Received unexpected result content of type {block.GetType()}");
    }
}

// There is only one ElicitationHandler for both form mode and URL mode elicitation,
// so the handler should begin by checking the `Mode` property of the `ElicitRequestParams` parameter
// and process the request appropriately.
// In this example, we only handle URL mode elicitation.
async ValueTask<ElicitResult> HandleElicitationAsync(ElicitRequestParams? requestParams, CancellationToken token)
{
    // Bail out if the requestParams is null or if the elicitation is not a valid URL mode request
    if (requestParams is null || requestParams.Mode != "url" || requestParams.Url is null)
    {
        return new ElicitResult();
    }

    // Process the elicitation request

    // MCP clients MUST:
    // - Provide UI that makes it clear which server is requesting information
    // - Respect user privacy and provide clear decline and cancel options
    // - For URL mode, clearly display the target domain/host and gather user consent before navigation to the target URL

    Console.WriteLine($"Elicitation Request Received from {mcpClient.ServerInfo.Name}, Version {mcpClient.ServerInfo.Version}");
    Console.WriteLine($"Elicitation URL: {requestParams.Url}");

    if (requestParams.Message is not null)
    {
        Console.WriteLine(requestParams.Message);
    }

    // Ask the user if they accept, decline, or cancel the elicitation
    Console.Write("Do you accept the elicitation request? (yes/no/cancel): ");
    var userResponse = Console.ReadLine();
    if (string.Equals(userResponse?.Trim(), "no", StringComparison.OrdinalIgnoreCase))
    {
        return new ElicitResult
        {
            Action = "decline"
        };
    }
    else if (string.Equals(userResponse?.Trim(), "cancel", StringComparison.OrdinalIgnoreCase))
    {
        return new ElicitResult
        {
            Action = "cancel"
        };
    }

    // Open a browser to the elicitation URL
    try
    {
        using var process = new Process();
        process.StartInfo.UseShellExecute = true;
        process.StartInfo.FileName = requestParams.Url;
        process.Start();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to open browser: {ex.Message}");
        return new ElicitResult
        {
            Action = "cancel"
        };
    }

    // Return the user's input
    return new ElicitResult
    {
        Action = "accept"
    };
}
