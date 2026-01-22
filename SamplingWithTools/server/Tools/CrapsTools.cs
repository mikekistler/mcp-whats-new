using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

/// <summary>
/// MCP tools for playing the dice game Craps.
/// </summary>
internal class CrapsTools
{
    private readonly McpServer _mcpServer;
    private readonly ILogger<CrapsTools> _logger;

    public CrapsTools(McpServer mcpServer, ILogger<CrapsTools> logger)
    {
        _mcpServer = mcpServer;
        _logger = logger;
    }

    [McpServerTool]
    [Description("Plays a complete game of Craps using standard casino rules. Uses sampling to request dice rolls from the client. Returns the outcome and roll history.")]
    public async Task<string> PlayCraps(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PlayCraps: Entry");

        if (_mcpServer?.ClientCapabilities?.Sampling?.Tools is not {})
        {
            _logger.LogWarning("PlayCraps: Exit - client does not support sampling with tools");
            return "Error: Client does not support sampling with tools.";
        }

        // Create an IChatClient that wraps sampling and automatically handles tool invocation
        IChatClient chatClient = ChatClientBuilderChatClientExtensions.AsBuilder(_mcpServer.AsSamplingChatClient())
            .UseFunctionInvocation()
            .Build();

        // Define the roll_die tool as an AIFunction
        AIFunction rollDieTool = AIFunctionFactory.Create(
            () => Random.Shared.Next(1, 7),
            name: "roll_die",
            description: "Rolls a single six-sided die and returns the result (1-6)."
        );

        var chatOptions = new ChatOptions
        {
            Tools = [rollDieTool],
            ToolMode = ChatToolMode.Auto
        };

        StringBuilder result = new();
        result.AppendLine("Starting a game of Craps...");

        // Come out roll
        var pointRollResponse = await chatClient.GetResponseAsync(
            "We are playing a standard game of craps. Roll the dice to establish the point and return the point as a single number. Or return the game result (win/lose).",
            chatOptions,
            cancellationToken
        );

        var pointRollText = pointRollResponse.Text ?? "No response";
        result.AppendLine($"Come out roll response: {pointRollText}");

        // The response will be one of the following:
        // "Player wins on the come out roll!"  -- sum is 7 or 11
        // "Player loses on the come out roll!" -- sum is 2, 3, or 12
        // "Point is established at X."         -- sum is 4, 5, 6, 8, 9, or 10

        // Handle these cases.
        if (pointRollText.Contains("win", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("PlayCraps: Exit - player wins on come out");
            return result.ToString();
        }
        else if (pointRollText.Contains("lose", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("PlayCraps: Exit - player loses on come out");
            return result.ToString();
        }

        // Extract the point from the response
        int point = 0;
        var words = pointRollText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            if (int.TryParse(word.Trim('.', ',', ':', '!', '?'), out int value) && value >= 4 && value <= 10)
            {
                point = value;
                break;
            }
        }

        if (point == 0)
        {
            result.AppendLine("Error: Could not extract point from come out roll response.");
            _logger.LogInformation("PlayCraps: Exit - error extracting point");
            return result.ToString();
        }

        result.AppendLine($"Point is established at {point}.");

        // Point phase
        while (true)
        {
            var pointPhaseResponse = await chatClient.GetResponseAsync(
                $"We are playing a standard game of craps. The player has already thrown the come out roll and the point is {point}. Roll the dice and report if the player has won, lost, or should continue.",
                chatOptions,
                cancellationToken
            );

            var pointPhaseText = pointPhaseResponse.Text ?? "No response";
            result.AppendLine($"Point phase roll response: {pointPhaseText}");

            // Check if game ended
            if (pointPhaseText.Contains("win", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("PlayCraps: Exit - player wins by making point");
                return result.ToString();
            }
            else if (pointPhaseText.Contains("lose", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("PlayCraps: Exit - player loses by rolling 7");
                return result.ToString();
            }

            // Otherwise continue rolling
        }
    }
}
