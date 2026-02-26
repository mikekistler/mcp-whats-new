# Incremental Scope Consent with Protected MCP Server Sample

This sample demonstrates how an MCP server can inform clients of the scopes needed for access to specific tools and resources.

The code in this sample is derived from the Protected MCP Server and Client samples in the MCP C# SDK repo, with modifications to demonstrate incremental scope consent using the `WWW-Authenticate` header.

## Overview

The MCP Server provides several weather-related tools that require authentication. One tool, `make_it_rain`, requires the `rain:god` scope, which is not one of the default scopes granted to clients. When a client without the `rain:god` scope attempts to call the `make_it_rain` tool, the server responds with a `403 Forbidden` status and includes a `WWW-Authenticate` header indicating the required scope. The client can then request consent for the additional scope and retry the request.

For the purposes of the demo, the server also provides a `get_user_info` tool that returns the scopes associated with the client's access token, allowing you to see when the `rain:god` scope has been granted. This is used in the client sample to show the change in scopes before and after requesting consent.

## Prerequisites

- .NET 10.0 or later
- Enable HTTPS development certificates (for the OAuth server)
- A running TestOAuthServer (for OAuth authentication)

## Setup and Running

### Step 1: Start the Test OAuth Server

First, you need to start the TestOAuthServer which issues access tokens:

```bash
cd oAuthServer
dotnet dev-certs https --trust
dotnet run --framework net10.0
```

The OAuth server will start at `https://localhost:7029`

### Step 2: Start the Protected MCP Server

Run this server:

```bash
cd server
dotnet run
```

The server will start at `http://localhost:7071`

### Step 3: Test with MCP Client

You can test the server using the demo MCP Client:

```bash
cd client
dotnet run
```

## What Happens

1. The client connects to the MCP server at `http://localhost:7071`

    ```text
    Connecting to weather server at http://localhost:7071/...
    ```

1. The client discovers that it needs to authenticate and obtains an access token from the OAuth server at `https://localhost:7029/` with the default scopes (e.g., `mcp:tools`). You will probably see a browser window open as part of the OAuth authorization flow.

    ```text
    Starting OAuth authorization flow...
    ...
    Authorization code received successfully.
    ```

1. The client calls the `get_alerts` tool, which succeeds because it does not require special scopes

    ```text
    Calling get_alerts tool...
    Result: Event: Winter Weather Advisory
    ...
    ```

1. After the successful call, the client calls the `get_user_info` tool and displays the scopes associated with the access token (initially just `mcp:tools`)

    ```text
    Calling get_user_info tool...
    Result: {"user":"user-dyn-a0046d72b3fb45dfad4ad3c2083f1322","scopes":["mcp:tools"]}
    ````

1. The client calls the `make_it_rain` tool, which fails with a `403 Forbidden` status and a `WWW-Authenticate` header indicating that the `rain:god` scope is required. You can see the `403` response in the server log:

    ```text
    info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
          Request starting HTTP/1.1 POST http://localhost:7071/ - application/json;+charset=utf-8 -
    Token validated for: user-dyn-21fdaa3b7d534aed8181f0ca2d8d7ab4 (unknown)
    warn: ProtectedMcpServer.Middleware.CheckScopesMiddleware[0]
          Scope check failed for tool call make_it_rain. Missing required scope rain:god. User: user-dyn-21fdaa3b7d534aed8181f0ca2d8d7ab4
    info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
          Request finished HTTP/1.1 POST http://localhost:7071/ - 403 - - 0.8502ms
    ```

1. The SDK parses this header and automatically requests consent for the additional scope. This triggers the OAuth flow again, and you will see the browser window open again.

    ```text
    Starting OAuth authorization flow...
    ...
    Authorization code received successfully.
    ```

1. After the user consents to the additional scope, the client retries the `make_it_rain` tool call, which now succeeds because the access token has been updated with the new scope.

     ```text
    Result: It's now raining in WA! ☔️
    ```

1. Finally, the client calls the `get_user_info` tool again to show that the `rain:god` scope has now been granted to the access token after the incremental consent flow.

    ```text
    Calling get_user_info tool...
    Result: {"user":"user-dyn-a0046d72b3fb45dfad4ad3c2083f1322","scopes":["mcp:tools","rain:god"]}
    ```
