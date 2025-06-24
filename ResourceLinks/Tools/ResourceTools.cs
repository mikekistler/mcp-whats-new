using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ResourceLinks.Resources;

namespace ResourceLinks.Tools;

[McpServerToolType]
public sealed class ResourceTools
{
    [McpServerTool]
    public async Task<CallToolResult> MakeAResource()
    {
        int id = new Random().Next(1, 101); // 1 to 100 inclusive

        var resource = ResourceGenerator.CreateResource(id);

        var result = new CallToolResult();

        result.Content.Add(new ResourceLinkBlock()
        {
            Uri = resource.Uri,
            Name = resource.Name
        });

        return result;
    }
}