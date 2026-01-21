using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Register IHttpContextAccessor for accessing HTTP context in tools
builder.Services.AddHttpContextAccessor();

// Add AntiForgery services
builder.Services.AddAntiforgery();

// Add the MCP services: the transport to use (http) and the tools to register.
builder.Services
    .AddMcpServer(options =>
    {
        options.ProtocolVersion = "2025-11-25";
    })
    .WithHttpTransport()
    .WithTools<ElicitationTools>();

var app = builder.Build();

app.UseAntiforgery();

// Endpoint to display the elicitation form
app.MapGet("/elicitation-info", DisplayElicitationForm);

// Endpoint to receive the elicitation information
app.MapPost("/elicitation-info", HandleElicitationFormSubmission);

app.MapMcp();

app.Run();

// Pages that generate post requests should include antiforgery tokens to prevent CSRF attacks
static async Task<IResult> DisplayElicitationForm(string id, IAntiforgery antiforgery, HttpContext context)
{
    if (string.IsNullOrEmpty(id))
    {
        return TypedResults.BadRequest("Missing elicitation ID");
    }

    // Peek at the queue to verify the ID exists but don't remove it yet
    var requestExists = ElicitationTools.PeekRequest(id);

    if (requestExists == null)
    {
        return TypedResults.NotFound("Elicitation request not found");
    }

    // Generate antiforgery token
    var tokens = antiforgery.GetAndStoreTokens(context);
    var antiforgeryInput = $"<input type=\"hidden\" name=\"{tokens.FormFieldName}\" value=\"{tokens.RequestToken}\" />";

    // Display an HTML form to collect user input
    var formHtml = await File.ReadAllTextAsync("Pages/ElicitationForm.html");
    formHtml = formHtml.Replace("{{ELICITATION_ID}}", id);
    formHtml = formHtml.Replace("{{ANTIFORGERY_TOKEN}}", antiforgeryInput);

    return TypedResults.Content(formHtml, "text/html");
}

static async Task<IResult> HandleElicitationFormSubmission(
    string id,
    [FromForm] string name,
    [FromForm] string ssn,
    [FromForm] string secret)
{
    if (string.IsNullOrEmpty(id))
    {
        return TypedResults.BadRequest("Missing elicitation ID");
    }

    // Find and remove the matching request
    if (!ElicitationTools.TryRemoveRequest(id, out var matchingRequest))
    {
        return TypedResults.NotFound("Elicitation request not found");
    }

    // Create user data dictionary from form parameters
    var userData = new Dictionary<string, string>
    {
        ["name"] = name,
        ["ssn"] = ssn,
        ["secret"] =secret
    };

    // Store the data and signal completion
    matchingRequest.UserData = userData;
    matchingRequest.CompletionSource.SetResult(userData);

    // Return success page
    var successHtml = await File.ReadAllTextAsync("Pages/ElicitationSuccess.html");

    return TypedResults.Content(successHtml, "text/html");
}
