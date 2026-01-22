using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
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

        StringBuilder result = new();
        result.AppendLine("Starting a game of Craps...");

        Tool rollDieTool = new Tool()
        {
            Name = "roll_die",
            Description = "Rolls a single six-sided die and returns the result (1-6)."
        };

        // Come out roll
        var pointRollResponse = await SampleWithToolsAsync(
            new ()
            {
                Messages = [new() {
                    Role = ModelContextProtocol.Protocol.Role.User,
                    Content = [new TextContentBlock() { Text = "We are playing a standard game of craps. Roll the dice to establish the point and return the point as a single number. Or return the game result (win/lose)." }]
                }],
                MaxTokens = 1000,
                Tools = [rollDieTool],
                ToolChoice = new () { Mode = "auto" } // Let the model decide when to use the tool
            },
            cancellationToken
        );

        var pointRollText = pointRollResponse.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "No response";
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
            var pointPhaseResponse = await SampleWithToolsAsync(
                new ()
                {
                    Messages = [new() {
                        Role = ModelContextProtocol.Protocol.Role.User,
                        Content = [new TextContentBlock() { Text = $"We are playing a standard game of craps. The player has already thrown the come out roll and the point is {point}. Roll the dice and report if the player has won, lost, or should continue." }]
                    }],
                    MaxTokens = 1000,
                    Tools = [rollDieTool],
                    ToolChoice = new () { Mode = "auto" } // Let the model decide when to use the tool
                },
                cancellationToken
            );

            var pointPhaseText = pointPhaseResponse.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "No response";
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

    // Write a wrapper for SampleAsync that handles tool calls.
    public async Task<CreateMessageResult> SampleWithToolsAsync(CreateMessageRequestParams requestParams, CancellationToken cancellationToken = default)
    {
        const int maxIterations = 10;
        for (int i = 0; i < maxIterations && !cancellationToken.IsCancellationRequested; i++)
        {
            var result = await _mcpServer.SampleAsync(requestParams, cancellationToken);

            if (result.StopReason != "toolUse")
            {
                Console.WriteLine($"Sampling completed with result {result.StopReason ?? "unknown"}.");
                return result;
            }

            // Note that the LLM might return multiple tool uses in a single response.
            var toolUses = result.Content.OfType<ToolUseContentBlock>().ToList();

            // Ensure we have a tool use to process.
            if (toolUses.Count == 0)
            {
                Console.WriteLine("Error: Expected tool use content block but none found.");
                return result;
            }

            // Push the result content back into the conversation.
            requestParams.Messages.Add(new SamplingMessage()
            {
                Role = ModelContextProtocol.Protocol.Role.Assistant,
                Content = result.Content.ToList(),
            });

            // Now execute each tool -- in parallel if multiple.
            IList<ToolResultContentBlock> toolResults = (await Task.WhenAll(
                toolUses.Select(async (toolUse) =>
                {
                    return await CallToolAsync(toolUse, cancellationToken);
                })
            )).ToList<ToolResultContentBlock>();

            requestParams.Messages.Add(new SamplingMessage()
            {
                Role = ModelContextProtocol.Protocol.Role.User,
                Content = toolResults.Cast<ContentBlock>().ToList(),
            });
        }
        throw new InvalidOperationException("Maximum number of tool use iterations reached.");
    }

    public async Task<ToolResultContentBlock> CallToolAsync(ToolUseContentBlock toolUse, CancellationToken cancellationToken = default)
    {
        string toolName = toolUse.Name;
        _logger.LogInformation("CallToolAsync: Entry for tool {ToolName}", toolName);

        switch (toolName)
        {
            case "roll_die":
                int rollResult = Random.Shared.Next(1, 7);
                var contentBlock = new TextContentBlock() { Text = rollResult.ToString() };
                _logger.LogInformation("CallToolAsync: Exit for tool {ToolName} with result {RollResult}", toolName, rollResult);
                return new ToolResultContentBlock
                {
                    ToolUseId = toolUse.Id,
                    Content = [contentBlock],
                };

            default:
                _logger.LogWarning("CallToolAsync: Unknown tool {ToolName}", toolName);
                throw new ArgumentException($"Unknown tool: {toolName}", nameof(toolName));
        }
    }
}
