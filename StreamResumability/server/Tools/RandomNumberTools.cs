using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

/// <summary>
/// Sample MCP tools for demonstration purposes.
/// These tools can be invoked by MCP clients to perform various operations.
/// </summary>
internal class RandomNumberTools
{
    private static int delayInSeconds = 0;
    private static int retryIntervalInSeconds = 0;

    [McpServerTool]
    [Description("Sets the delay in seconds for simulating long-running operations.")]
    public string SetDelay(
        [Description("Delay in seconds")] int delayInSeconds,
        [Description("Retry interval in seconds")] int retryIntervalInSeconds)
    {
        RandomNumberTools.delayInSeconds = delayInSeconds;
        RandomNumberTools.retryIntervalInSeconds = retryIntervalInSeconds;
        return $"Delay set to {delayInSeconds} seconds and retry interval set to {retryIntervalInSeconds} seconds";
    }

    [McpServerTool]
    [Description("Generates a random number between the specified minimum and maximum values.")]
    public async Task<int> GetRandomNumber(
        RequestContext<CallToolRequestParams> context,
        [Description("Minimum value (inclusive)")] int min = 0,
        [Description("Maximum value (exclusive)")] int max = 100)
    {
        // If a retry interval is set, set up client polling
        if (retryIntervalInSeconds > 0)
        {
            // Server disconnects the stream after emitting event with retryAfter set
            await context.EnablePollingAsync(retryInterval: TimeSpan.FromSeconds(retryIntervalInSeconds));
        }

        // Add a delay to simulate a long-running operation
        await Task.Delay(delayInSeconds * 1000);
        return Random.Shared.Next(min, max);
    }
}
