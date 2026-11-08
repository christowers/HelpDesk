using HelpDesk.Api;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using System.ClientModel;
using System.Data.Common;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddTransient<PolicyAnswerService>();
var databaseProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var useSqlite = string.Equals(databaseProvider, "Sqlite", StringComparison.OrdinalIgnoreCase);
if (useSqlite)
{
    var databasePath = Path.GetFullPath(builder.Configuration["Database:SqlitePath"] ?? "Data/HelpDesk.db",
        builder.Environment.ContentRootPath);
    Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
    var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    builder.Services.AddDbContext<PolicyDbContext>(options => options.UseSqlite(connectionString));
}
else if (string.Equals(databaseProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
{
    var connectionString = builder.Configuration.GetConnectionString("HelpDesk")
        ?? throw new InvalidOperationException("Set the HelpDesk connection string.");
    builder.Services.AddDbContext<PolicyDbContext>(options => options.UseSqlServer(connectionString));
}
else
{
    throw new InvalidOperationException("Database:Provider must be Sqlite or SqlServer.");
}
builder.Services.AddScoped<PolicyStore>();
builder.Services.AddScoped<SoftwareCatalog>();

var mode = builder.Configuration["HelpDesk:Mode"] ?? "OpenAI";
switch (mode.ToLowerInvariant())
{
    case "offline":
        builder.Services.AddSingleton<IPolicyModel, OfflinePolicyModel>();
        break;
    case "openai":
        var apiKey = builder.Configuration["OPENAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Set OPENAI_API_KEY before using OpenAI mode.");
        }

        var model = builder.Configuration["OPENAI_MODEL"] ?? "gpt-4o-mini";
        builder.Services.AddSingleton(new ChatClient(model, apiKey));
        builder.Services.AddSingleton<IPolicyModel, OpenAiPolicyModel>();
        break;
    default:
        throw new InvalidOperationException("HelpDesk:Mode must be Offline or OpenAI.");
}

var app = builder.Build();
if (useSqlite)
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<PolicyDbContext>();
    await database.InitializeSqliteAsync();
}

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/helpdesk/ask", async (
    AskRequest request, PolicyAnswerService service, HttpContext context, CancellationToken cancellationToken) =>
{
    var question = request.Question?.Trim();
    if (string.IsNullOrEmpty(question) || question.Length > 1_000)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["question"] = ["Enter a question between 1 and 1,000 characters."]
        });
    }

    context.Response.Headers["X-HelpDesk-Mode"] = mode;
    try
    {
        var answer = await service.AnswerAsync(question, cancellationToken);
        return Results.Ok(answer);
    }
    catch (DbException ex) when (ex is SqlException or SqliteException)
    {
        var errorCode = ex is SqlException sqlError ? sqlError.Number : ((SqliteException)ex).SqliteErrorCode;
        app.Logger.LogWarning("Policy database request failed with {Provider} error {Number}.", databaseProvider, errorCode);
        return Results.Problem(statusCode: 503, title: "The policy database is unavailable.");
    }
    catch (ClientResultException ex)
    {
        app.Logger.LogWarning("Model provider request failed with status {Status}.", ex.Status);
        return Results.Problem(statusCode: 502, title: "The model provider request failed.");
    }
    catch (HttpRequestException)
    {
        app.Logger.LogWarning("The model provider could not be reached.");
        return Results.Problem(statusCode: 502, title: "The model provider could not be reached.");
    }
    catch (InvalidModelResponseException)
    {
        return Results.Problem(statusCode: 502, title: "The model returned an unusable answer.");
    }
});

app.Run();

public partial class Program { }
