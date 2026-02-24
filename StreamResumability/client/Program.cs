using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Diagnostics;

// Parse command line argument for delay in seconds
int delayInSeconds = 0;
if (args.Length > 0 && int.TryParse(args[0], out int parsedDelay))
{
    delayInSeconds = parsedDelay;
}

// Parse command line argument for retry interval
int retryIntervalInSeconds = 0;
if (args.Length > 1 && int.TryParse(args[1], out int parsedRetryInterval))
{
    retryIntervalInSeconds = parsedRetryInterval;
}

var endpoint = Environment.GetEnvironmentVariable("ENDPOINT") ?? "http://localhost:6173";

var clientTransport = new HttpClientTransport(new()
{
    Endpoint = new Uri(endpoint),
    TransportMode = HttpTransportMode.StreamableHttp,
});

McpClientOptions options = new()
{
    ClientInfo = new()
    {
        Name = "CSharpClient",
        Version = "1.0.0"
    }
};

await using var mcpClient = await McpClient.CreateAsync(clientTransport, options);

var tools = await mcpClient.ListToolsAsync();
Console.WriteLine($"Connected to server with tools: {string.Join(", ", tools.Select(t => t.Name))}");

// Set delay

Console.WriteLine($"Setting delay to {delayInSeconds} seconds and retry interval to {retryIntervalInSeconds} seconds");
var setDelayArgs = new Dictionary<string, object?>
{
    { "delayInSeconds", delayInSeconds },
    { "retryIntervalInSeconds", retryIntervalInSeconds }
};
await mcpClient.CallToolAsync(toolName: "set_delay", arguments: setDelayArgs);

// Call the get_random_number tool and print the result and duration of the call

{
    Console.WriteLine($"Calling get_random_number");

    var stopwatch = Stopwatch.StartNew();
    var numberResult = await mcpClient.CallToolAsync(toolName: "get_random_number");
    stopwatch.Stop();

    Console.Write($"Result of get_random_number call: ");
    var textBlock = numberResult.Content.OfType<TextContentBlock>().First();
    Console.WriteLine(textBlock.Text);

    Console.WriteLine($"get_random_number call completed in {stopwatch.ElapsedMilliseconds / 1000.0} s");
}
