# mcp-whats-new

New features in MCP C# SDK for the [2025-11-25 version of the MCP Specification]:

- [Enhance authorization server discovery with support for OpenID Connect Discovery 1.0](#enhance-authorization-server-discovery-with-support-for-openid-connect-discovery-100)

- [Icons for tools, resources, resource templates, and prompts](#icons-for-tools-resources-resource-templates-and-prompts)

- [Incremental scope consent via WWW-Authenticate](#incremental-scope-consent-via-www-authenticate)

- [Added support for URL mode elicitation](#added-support-for-url-mode-elicitation)

- [Add tool calling support to sampling](#add-tool-calling-support-to-sampling)

- [Add support for OAuth Client ID Metadata Documents](#add-support-for-oauth-client-id-metadata-documents)

- [Support for long-running requests over HTTP with polling](#support-for-long-running-requests-over-http-with-polling)

- [An experimental tasks feature for durable requests with polling and deferred result retrieval](#an-experimental-tasks-feature-for-durable-requests-with-polling-and-deferred-result-retrieval)

See the [Changelog] for the full list of changes.

[2025-11-25 version of the MCP Specification]: https://modelcontextprotocol.io/specification/2025-11-25
[Changelog]: https://modelcontextprotocol.io/specification/2025-11-25/changelog

## Enhance authorization server discovery with support for OpenID Connect Discovery 1.0

- Spec change: [PR #797](https://github.com/modelcontextprotocol/modelcontextprotocol/pull/797)
- SDK change: [PR #377](https://github.com/modelcontextprotocol/csharp-sdk/pull/377)

The primary change is that in the 06-18 spec, the server was required to provide a link to its
Protected Resource Metadata (PRM) Document in the `resource_metadata` parameter ofthe WWW-Authenticate header.
In the 11-25 spec, the server can expose the PRM in any of three ways:

1. Via a URL in the `resource_metadata` parameter of the WWW-Authenticate header (as before)
2. At a "well known" URL
    - At the path of the server’s MCP endpoint: https://example.com/public/mcp could host metadata at https://example.com/.well-known/oauth-protected-resource/public/mcp
    - At the root: https://example.com/.well-known/oauth-protected-resource

Clients check for the PRM in these locations in the order listed above.

The MCP C# SDK provides the `AddMcp` extension method of `AuthenticationBuilder` for the server to specify the key fields
for the PRM Document.

```csharp
    .AddMcp(options =>
    {
        options.ResourceMetadata = new()
        {
            ResourceDocumentation = new Uri("https://docs.example.com/api/weather"),
            AuthorizationServers = { new Uri(inMemoryOAuthServerUrl) },
            ScopesSupported = ["mcp:tools"],
        };
    });
```

When the server is configured this way, the SDK hosts the PRM Document at the well-known
location "/.well-known/oauth-protected-resource/" and includes this link in the
`resource_metadata` parameter of the WWW-Authenticate header.

The MCP C# client SDK automatically uses the sequence of steps above to discover the PRM Document and then uses it
to obtain the necessary authorization.

## Icons for tools, resources, resource templates, and prompts

- Spec change: [SEP-973](https://modelcontextprotocol/modelcontextprotocol/issues/973)
- SDK change: [PR #802](https://github.com/modelcontextprotocol/csharp-sdk/pull/802)

The 2025-11-25 version of the MCP Specification adds a list of icons to the metadata for Tools, Resources, Prompts.
This icon information is included in the response of a `tools/list`, `resources/list`, or `prompts/list` request.
The Implementation metadata, which describes either a client or a server, has also been extended to include icons and a website URL.
The C# SDK has been updated to support these fields.

The `IconSource` parameter of `McpServerToolAttribute` can be used to specify a single icon URL for a tool.
An example usage is shown below.

```csharp
[McpServerTool(Title = "This is a title", IconSource = "https://example.com/tool-icon.svg")]
public static string ToolWithIcon(
```

The `McpServerResourceAttribute`, `McpServerResourceTemplateAttribute`, and `McpServerPromptAttribute` also have an `IconSource` parameter that can be used similarly to specify a single icon URL for resources, resource templates, and prompts, respectively.

For more advanced icon configuration (multiple icons, MIME type specification, size characteristics),
icons can be specified for a tool programmatically, for example using the `McpServerToolCreateOptions.Icons` property:

```csharp
    .WithTools([
        // EchoTool with complex icon configuration demonstrating multiple icons,
        // MIME types, size specifications, and theme preferences
        McpServerTool.Create(
            typeof(EchoTool).GetMethod(nameof(EchoTool.Echo))!,
            options: new McpServerToolCreateOptions
            {
                Icons = [
                    // High-resolution PNG icon for light theme
                    new Icon
                    {
                        Source = "https://raw.githubusercontent.com/microsoft/fluentui-emoji/main/assets/Loudspeaker/Flat/loudspeaker_flat.svg",
                        MimeType = "image/svg+xml",
                        Sizes = ["any"],
                        Theme = "light"
                    },
                    // 3D icon for dark theme
                    new Icon
                    {
                        Source = "https://raw.githubusercontent.com/microsoft/fluentui-emoji/main/assets/Loudspeaker/3D/loudspeaker_3d.png",
                        MimeType = "image/png",
                        Sizes = ["256x256"],
                        Theme = "dark"
                    },
                    // WebP format for modern browsers, inline Data URI
                    new Icon
                    {
                        Source = "data:image/webp;base64,UklGRiQAAABXRUJQVlA4IBgAAAAwAQCdASoBAAEAAwA0JaQAA3AA/vuUAAA=",
                        MimeType = "image/webp",
                        Sizes = ["32x32"]
                    }
                ]
            }
        )
    ])
```

The `McpServerResourceCreateOptions`, `McpServerResourceTemplateCreateOptions`, and `McpServerPromptCreateOptions` classes have similar `Icons` properties for specifying icons for resources, resource templates, and prompts, respectively.

Finally, the `Implementation` class also has an `Icons` property that can be used to specify icons,
and it has a `Website` property to specify a website URL for the implementation metadata.
These can be set in the `Implementation` object used in the server or client configuration:

```csharp
    .AddMcpServer(options =>
    {
        // Configure server implementation details with icons and website
        options.ServerInfo = new Implementation
        {
            Name = "Everything Server",
            Version = "1.0.0",
            Title = "MCP Everything Server",
            Description = "A comprehensive MCP server demonstrating tools, prompts, resources, sampling, and all MCP features",
            WebsiteUrl = "https://github.com/modelcontextprotocol/csharp-sdk",
            Icons = [
                new Icon
                {
                    Source = "https://raw.githubusercontent.com/microsoft/fluentui-emoji/main/assets/Gear/Flat/gear_flat.svg",
                    MimeType = "image/svg+xml",
                    Sizes = ["any"],
                    Theme = "light"
                },
                new Icon
                {
                    Source = "https://raw.githubusercontent.com/microsoft/fluentui-emoji/main/assets/Gear/3D/gear_3d.png",
                    MimeType = "image/png",
                    Sizes = ["256x256"]
                }
            ]
        };
    })
```

## Incremental scope consent via WWW-Authenticate

- Spec change: [SEP-835](https://modelcontextprotocol/modelcontextprotocol/issues/835)
- SDK change: In progress

## Added support for URL mode elicitation

- Spec change: [SEP-1036](https://modelcontextprotocol/modelcontextprotocol/issues/1036)
- SDK change: [PR #1021](https://github.com/modelcontextprotocol/csharp-sdk/pull/1021)

## Add tool calling support to sampling

- Spec change: [SEP-1577](https://modelcontextprotocol/modelcontextprotocol/issues/1577)
- SDK change: [PR #976](https://github.com/modelcontextprotocol/csharp-sdk/pull/976)

## Add support for OAuth Client ID Metadata Documents

- Spec change: [SEP-991](https://modelcontextprotocol/modelcontextprotocol/issues/991)
- SDK change: [PR #1023](https://github.com/modelcontextprotocol/csharp-sdk/pull/1023)

## Support for long-running requests over HTTP with polling

- Spec change: [SEP-1699](https://modelcontextprotocol/modelcontextprotocol/issues/1699)
- SDK change: In progress

## An experimental tasks feature for durable requests with polling and deferred result retrieval

- Spec change: [SEP-1686](https://modelcontextprotocol/modelcontextprotocol/issues/1686)
- SDK change: In progress
