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

[2025-11-25 version of the MCP Specification]: https://github.com/modelcontextprotocol.io/specification/2025-11-25
[Changelog]: https://github.com/modelcontextprotocol.io/specification/2025-11-25/changelog

## Enhance authorization server discovery with support for OpenID Connect Discovery 1.0

- Spec change: [PR #797](https://github.com/modelcontextprotocol/modelcontextprotocol/pull/797)
- SDK change: [PR #377](https://github.com/modelcontextprotocol/csharp-sdk/pull/377)

The primary change is that in the 2025-06-18 spec, the server was required to provide a link to its
Protected Resource Metadata (PRM) Document in the `resource_metadata` parameter of the `WWW-Authenticate` header.
In the 2025-11-25 spec, the server can expose the PRM in any of three ways:

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

- SEP: [SEP-973](https://github.com/modelcontextprotocol/modelcontextprotocol/issues/973)
- Spec change: [PR #955](https://github.com/modelcontextprotocol/modelcontextprotocol/pull/955)
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

- Spec change: [SEP-835](https://github.com/modelcontextprotocol/modelcontextprotocol/pull/835)
- SDK change: [PR #1084](https://github.com/modelcontextprotocol/csharp-sdk/pull/1084) <!-- TODO: Check -->

Another important feature of the 2025-11-25 version of the MCP Specification is support for incremental scope consent via the `WWW-Authenticate` header.
This feature supports the [Principle of Least Privilege] by allowing clients to request only the minimum level of access needed to perform their tasks.

[Principle of Least Privilege]: https://en.wikipedia.org/wiki/Principle_of_least_privilege

MCP uses OAuth 2.0 for authorization, and in Oauth 2.0, _scopes_ define the level of access a client has to a resource.
Clients request permissions to specific scopes during the authorization process.
With incremental scope consent, a client can start with minimal scopes and request additional scopes as needed.

Since the 2025-06-18 version of the MCP Specification, MCP servers must expose a Protected Resource Metadata (PRM) Document
that may include a `scopes_supported` property that lists the scopes they support.
But clients may not know which scopes are required for specific operations, so might request access to all possible scopes upfront.
The 2025-11-25 version of the MCP Specification describes recommended practices that let MCP clients request access for a minimal set of scopes initially,
and then request additional scopes as needed.

The key practices are:

- **Communicating the initial set of scopes**: When a client makes a request to an MCP server without an `Authorization` header, the server responds with a `401 Unauthorized` status code and includes a `WWW-Authenticate` header. Previously this header included a `resource_metadata` parameter pointing to the PRM Document. Now, it can also include a `scopes` parameter that lists the scopes required for the requested operation.
Clients should request authorization for only these scopes in order to perform the operation.

- **Communicating additional scopes**: When a client makes a request with an `Authorization` header but lacks the necessary scopes for the requested operation, the server responds with a `403 Forbidden` status code. The response should include a `WWW-Authenticate` header with

  - an `error` parameter indicating `insufficient_scope`, and
  - a `scopes` parameter that lists the additional scopes required.

  Clients will typically request a new access token with the scopes specified in the `scopes` parameter and then retry the operation.
  Clients then typically continue using this new access token for subsequent requests until it expires or they encounter another `403 Forbidden` response.

Note that both the client and server must support these practices for incremental scope consent to work effectively.
Here's how the C# SDK helps accomplish this.

### Client support

The MCP C# client SDK automatically handles `WWW-Authenticate` headers with `scopes` parameters in `401 Unauthorized` and `403 Forbidden` responses.
When the client receives such a response, it extracts the required scopes from the `scopes` parameter and requests authorization for those scopes from the authorization server.

### Server support





## Added support for URL mode elicitation

- SEP: [SEP-1036](https://github.com/modelcontextprotocol/modelcontextprotocol/issues/1036)
- Spec change: [PR #887](https://github.com/modelcontextprotocol/modelcontextprotocol/pull/887)
- SDK change: [PR #1021](https://github.com/modelcontextprotocol/csharp-sdk/pull/1021)

The new URL mode elicitation feature enables secure out-of-band interactions between the server and end-user, bypassing the MCP host/client.
This is particularly valuable for situations requiring the gathering of sensitive data -— like API keys, third-party authorizations,
and payment information—that would otherwise pose a security risk if transmitted through the client.
Inspired by web security standards such as OAuth, this mechanism allows the MCP client to obtain user consent and direct the user’s browser
to a secure server-hosted URL, where the sensitive interaction takes place away from the client application.

To ensure the security and integrity of the elicitation process, the MCP host/client must present the elicitation request to the user,
including the identity of the server requesting the information and the purpose of the elicitation, and provide options for the user
to decline or cancel the elicitation process.
When the user accepts the elicitation request, the client navigate to the provided URL in a secure manner where the user can complete the necessary interaction.

Support for URL mode elicitation is an optional feature of MCP hosts/clients, and clients indicate their support for this feature
in the client capabilities sent in the connection initialization.
Servers should implement fallback mechanisms to handle cases where the client does not support this feature.

### Client support for URL mode elicitation

In the C# SDK, clients can indicate support for URL mode elicitation by setting the `Url` property of `Capabilities.Elicitation`
in the `McpClientOptions` object used to construct the `McpClient`, as shown below:

```csharp
McpClientOptions options = new()
{
    // ... other options ...
    Capabilities = new ClientCapabilities
    {
        Elicitation = new ElicitationCapability
        {
            Url = new UrlElicitationCapability()
        }
    }
```

As with form mode elicitation, the client must provide an `ElicitationHandler` in `McpClientOptions.Handlers`
when constructing the `McpClient`.

```csharp
McpClientOptions options = new()
{
    // ... other options ...
    Handlers = new()
    {
        ElicitationHandler = HandleElicitationAsync,
    }
```

There is only one `ElicitationHandler` for both form mode and URL mode elicitation,
so the handler should begin by checking the `Mode` property of the `ElicitationRequest` parameter to determine which mode is being requested
and handle it accordingly.

```csharp
async ValueTask<ElicitResult> HandleElicitationAsync(ElicitRequestParams? requestParams, CancellationToken token)
{
    // Bail out if the requestParams is null or if the elicitation is not a valid URL mode request
    if (requestParams is null || requestParams.Mode != "url" || requestParams.Url is null)
    {
        return new ElicitResult();
    }
```

### Server support for URL mode elicitation



## Add tool calling support to sampling

- SEP: [SEP-1577](https://github.com/modelcontextprotocol/modelcontextprotocol/issues/1577)
- SDK change: [PR #976](https://github.com/modelcontextprotocol/csharp-sdk/pull/976)

## Add support for OAuth Client ID Metadata Documents

- SEP: [SEP-991](https://github.com/modelcontextprotocol/modelcontextprotocol/issues/991)
- Spec change: [PR #1296](https://github.com/modelcontextprotocol/modelcontextprotocol/pull/1296)
- SDK change: [PR #1023](https://github.com/modelcontextprotocol/csharp-sdk/pull/1023)



[OAuth Client ID Metadata Documents](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-client-id-metadata-document-00)

## Support for long-running requests over HTTP with polling

- SEP: [SEP-1699](https://github.com/modelcontextprotocol/modelcontextprotocol/issues/1699)
- SDK change: In progress

## An experimental tasks feature for durable requests with polling and deferred result retrieval

- SEP: [SEP-1686](https://github.com/modelcontextprotocol/modelcontextprotocol/issues/1686)
- SDK change: In progress
