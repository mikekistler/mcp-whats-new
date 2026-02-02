using System.ComponentModel;
using ModelContextProtocol.Server;

/// <summary>
/// Sample MCP tools for demonstration purposes.
/// These tools can be invoked by MCP clients to perform various operations.
/// </summary>
internal class RandomNumberTools
{
    private static int delayInSeconds = 0;

    [McpServerTool]
    [Description("Sets the delay in seconds for simulating long-running operations.")]
    public string SetDelay(
        [Description("Delay in seconds")] int delayInSeconds)
    {
        RandomNumberTools.delayInSeconds = delayInSeconds;
        return $"Delay set to {delayInSeconds} seconds";
    }

    [McpServerTool]
    [Description("Generates a random number between the specified minimum and maximum values.")]
    public int GetRandomNumber(
        [Description("Minimum value (inclusive)")] int min = 0,
        [Description("Maximum value (exclusive)")] int max = 100)
    {
        // Add a delay to simulate a long-running operation
        System.Threading.Thread.Sleep(delayInSeconds * 1000);
        return Random.Shared.Next(min, max);
    }
}
