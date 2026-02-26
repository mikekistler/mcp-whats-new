using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Protocol;
using ProtectedMcpServer.Middleware;
using ProtectedMcpServer.Tools;
using System.Net.Http.Headers;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

var serverUrl = "http://localhost:7071/";
var inMemoryOAuthServerUrl = "https://localhost:7029";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = McpAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Configure to validate tokens from our in-memory OAuth server
    options.Authority = inMemoryOAuthServerUrl;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidAudience = serverUrl, // Validate that the audience matches the resource metadata as suggested in RFC 8707
        ValidIssuer = inMemoryOAuthServerUrl,
        NameClaimType = "name",
        RoleClaimType = "roles"
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var name = context.Principal?.Identity?.Name ?? "unknown";
            var email = context.Principal?.FindFirstValue("preferred_username") ?? "unknown";
            Console.WriteLine($"Token validated for: {name} ({email})");
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            Console.WriteLine($"Challenging client to authenticate with Entra ID");
            return Task.CompletedTask;
        }
    };
})
.AddMcp(options =>
{
    options.ResourceMetadata = new()
    {
        ResourceDocumentation = "https://docs.example.com/api/weather",
        AuthorizationServers = { inMemoryOAuthServerUrl },
        ScopesSupported = ["mcp:tools"],
    };
});

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();

builder.Services.AddMcpServer()
    .WithTools<WeatherTools>()
    .WithTools<ContextTools>()
    .WithHttpTransport();

// Configure HttpClientFactory for weather.gov API
builder.Services.AddHttpClient("WeatherApi", client =>
{
    client.BaseAddress = new Uri("https://api.weather.gov");
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("weather-tool", "1.0"));
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Add middleware to intercept tools/call requests for MakeItRain and check for the "rain:god" scope.
// This must be done in middleware because the MCP handler flushes response headers before invoking tools,
// so setting the status code inside the tool method would cause an InvalidOperationException.
app.UseMiddleware<CheckScopesMiddleware>();

// Use the default MCP policy name that we've configured
app.MapMcp().RequireAuthorization();

Console.WriteLine($"Starting MCP server with authorization at {serverUrl}");
Console.WriteLine($"Using in-memory OAuth server at {inMemoryOAuthServerUrl}");
Console.WriteLine($"Protected Resource Metadata URL: {serverUrl}.well-known/oauth-protected-resource");
Console.WriteLine("Press Ctrl+C to stop the server");

app.Run(serverUrl);
