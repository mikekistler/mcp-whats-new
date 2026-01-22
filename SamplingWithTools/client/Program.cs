
using System.ClientModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI;
using OpenAI.Chat;

var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
if (string.IsNullOrEmpty(token))
{
    Console.WriteLine("Error: GITHUB_TOKEN environment variable is not set.");
    return;
}
var baseUrl = "https://models.github.ai/inference";
var modelId = "gpt-4o-mini";

IChatClient chatClient =
    new OpenAIClient(new ApiKeyCredential(token), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
        .GetChatClient(modelId)
        .AsIChatClient();

var samplingHandler = chatClient.CreateSamplingHandler();

// Create the MCP client
// Configure it to connect to the SamplingWithTools MCP server.
var mcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new()
    {
        Endpoint = new Uri("http://localhost:6184"),
        Name = "SamplingWithTools MCP Server",
    }),
    clientOptions: new()
    {
        Capabilities = new ClientCapabilities
        {
            Sampling = new SamplingCapability
            {
                Tools = new SamplingToolsCapability {}
            }
        },
        Handlers = new()
        {
            SamplingHandler = async (c, p, t) =>
            {
                return await samplingHandler(c, p, t);
            },
        }
    });

// List all available tools from the MCP server.
Console.WriteLine("Available tools:");
IList<McpClientTool> tools = await mcpClient.ListToolsAsync();
foreach (McpClientTool tool in tools)
{
    Console.WriteLine($"{tool}");
}
Console.WriteLine();

// Conversational loop that can utilize the tools via prompts.
List<Microsoft.Extensions.AI.ChatMessage> messages = [];
while (true)
{
    Console.Write("Prompt: ");
    messages.Add(new(ChatRole.User, Console.ReadLine()));

    // Inner loop to handle tool calls
    while (true)
    {
        List<ChatResponseUpdate> updates = [];
        await foreach (ChatResponseUpdate update in chatClient
            .GetStreamingResponseAsync(messages, new() { Tools = [.. tools] }))
        {
            Console.Write(update);
            updates.Add(update);
        }
        Console.WriteLine();

        // Combine all content from the streaming updates
        var allContents = updates.SelectMany(u => u.Contents).ToList();

        // Add the assistant message with tool calls
        messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, allContents));

        // Check if there are any tool calls to execute
        var toolCalls = allContents.OfType<FunctionCallContent>().ToList();

        if (toolCalls.Count == 0)
        {
            // No tool calls - exit inner loop
            break;
        }

        // Execute all tool calls in parallel via the MCP server
        var toolCallTasks = toolCalls.Select(async toolCall =>
        {
            try
            {
                var toolResult = await mcpClient.CallToolAsync(toolCall.Name, toolCall.Arguments as IReadOnlyDictionary<string, object?>);
                var textResult = toolResult.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "No result";
                return new Microsoft.Extensions.AI.ChatMessage(ChatRole.Tool, [
                    new FunctionResultContent(toolCall.CallId, textResult)
                ]);
            }
            catch (Exception ex)
            {
                // Handle tool execution errors
                return new Microsoft.Extensions.AI.ChatMessage(ChatRole.Tool, [
                    new FunctionResultContent(toolCall.CallId, $"Error executing tool: {ex.Message}")
                ]);
            }
        });

        var toolResults = await Task.WhenAll(toolCallTasks);
        messages.AddRange(toolResults);

        // Continue inner loop to let model process tool results
    }
}
