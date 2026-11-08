using System.Net;
using System.Net.Http.Json;
using HelpDesk.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HelpDesk.Tests;

public sealed class HelpDeskEndpointTests : IClassFixture<HelpDeskApplication>
{
    private const string Route = "/api/helpdesk/ask";
    private const string ZoomQuestion = "Can I install Zoom on my work laptop?";
    private readonly WebApplicationFactory<Program> _app;

    public HelpDeskEndpointTests(HelpDeskApplication app)
    {
        _app = app.WithWebHostBuilder(builder => builder.UseSetting("HelpDesk:Mode", "Offline"));
    }

    [Fact]
    public async Task Home_page_serves_the_help_desk_form()
    {
        using var client = _app.CreateClient();
        using var response = await client.GetAsync("/");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var page = await response.Content.ReadAsStringAsync();
        Assert.Contains("<title>HelpDesk · IT Help Desk</title>", page);
        Assert.Contains("Ask the help desk", page);
        Assert.Contains(ZoomQuestion, page);
    }

    [Theory]
    [InlineData(ZoomQuestion, "Zoom is approved in the software catalog", "You may install it")]
    [InlineData("May I install BLENDER on the company desktop?", "lists Blender as requiring IT approval", "Get approval before installing it")]
    public async Task Installation_answer_uses_verified_catalog_status(string question, string status, string instruction)
    {
        using var client = _app.CreateClient();
        using var response = await client.PostAsJsonAsync(Route, new AskRequest(question));
        response.EnsureSuccessStatusCode();
        Assert.Equal("Offline", Assert.Single(response.Headers.GetValues("X-HelpDesk-Mode")));
        var answer = Assert.IsType<PolicyAnswer>(await response.Content.ReadFromJsonAsync<PolicyAnswer>());
        Assert.False(answer.InsufficientInformation);
        Assert.Equal("IT Policy §4.2", answer.Citation);
        Assert.Contains(status, answer.Answer);
        Assert.Contains(instruction, answer.Answer);
    }

    [Theory]
    [InlineData("Can I install ScreenFlow on my work laptop?")]
    [InlineData("Can I install Zoom Phone on my work laptop?")]
    [InlineData("Can I install Zoom and Blender on my work laptop?")]
    public async Task Unverified_product_gets_the_conditional_rule(string question)
    {
        using var app = _app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IPolicyModel>(new UnexpectedModel()))));
        using var client = app.CreateClient();
        using var response = await client.PostAsJsonAsync(Route, new AskRequest(question));

        var answer = await ReadConditionalInstallationAnswerAsync(response);
        Assert.Contains("couldn't verify that product", answer.Answer);
    }

    [Fact]
    public async Task Unavailable_catalog_gets_the_conditional_rule()
    {
        using var fixture = new HelpDeskApplication();
        using var app = fixture.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Database:Provider", "Sqlite");
            builder.UseSetting("HelpDesk:Mode", "Offline");
            builder.ConfigureServices(services =>
                services.Replace(ServiceDescriptor.Singleton<IPolicyModel>(new UnexpectedModel())));
        });
        using var client = app.CreateClient();
        var databasePath = app.Services.GetRequiredService<IConfiguration>()["Database:SqlitePath"];
        Assert.NotNull(databasePath);
        var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
        await using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "DROP TABLE SoftwareCatalog";
            await command.ExecuteNonQueryAsync();
        }

        using var response = await client.PostAsJsonAsync(Route, new AskRequest(ZoomQuestion));

        var answer = await ReadConditionalInstallationAnswerAsync(response);
        Assert.Contains("catalog is unavailable", answer.Answer);
    }

    [Fact]
    public async Task Verified_catalog_entry_reaches_the_model_with_the_policy()
    {
        using var app = _app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IPolicyModel>(new CatalogCheckingModel()))));
        using var client = app.CreateClient();
        using var response = await client.PostAsJsonAsync(Route, new AskRequest(ZoomQuestion));

        response.EnsureSuccessStatusCode();
        var answer = Assert.IsType<PolicyAnswer>(await response.Content.ReadFromJsonAsync<PolicyAnswer>());
        Assert.False(answer.InsufficientInformation);
        Assert.Equal("IT Policy §4.2", answer.Citation);
    }

    private static async Task<PolicyAnswer> ReadConditionalInstallationAnswerAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var answer = Assert.IsType<PolicyAnswer>(await response.Content.ReadFromJsonAsync<PolicyAnswer>());
        Assert.True(answer.InsufficientInformation);
        Assert.Equal("IT Policy §4.2", answer.Citation);
        Assert.Contains("If the software is listed in the approved catalog, you may install it on your work computer.", answer.Answer);
        Assert.Contains("Otherwise, get IT approval first.", answer.Answer);
        return answer;
    }

    [Fact]
    public async Task Blank_question_returns_bad_request()
    {
        using var client = _app.CreateClient();
        using var response = await client.PostAsJsonAsync(Route, new AskRequest("  "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("IT Policy §99")]
    [InlineData(null)]
    public async Task Definitive_answer_without_a_valid_citation_returns_bad_gateway(string? citation)
    {
        using var app = _app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IPolicyModel>(new FakeModel(citation)))));
        using var client = app.CreateClient();
        using var response = await client.PostAsJsonAsync(Route, new AskRequest(ZoomQuestion));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Theory]
    [InlineData("How long does it take help desk to reply to my request?", "IT Policy §2.1", "2 hours", false)]
    [InlineData("How long does it take IT to approve a software request?", "IT Policy §4.3", "no fixed approval timeline", true)]
    [InlineData("My VPN keeps dropping and will not reconnect.", "IT Policy §5.1", "VPN client", true)]
    public async Task Offline_answer_uses_the_matching_sql_policy(
        string question, string citation, string expectedText, bool insufficientInformation)
    {
        using var client = _app.CreateClient();
        using var response = await client.PostAsJsonAsync(Route, new AskRequest(question));

        response.EnsureSuccessStatusCode();
        var answer = Assert.IsType<PolicyAnswer>(await response.Content.ReadFromJsonAsync<PolicyAnswer>());
        Assert.Equal(citation, answer.Citation);
        Assert.Equal(insufficientInformation, answer.InsufficientInformation);
        Assert.Contains(expectedText, answer.Answer);
    }

    [Theory]
    [InlineData("What is on the cafeteria menu?")]
    [InlineData("?!")]
    [InlineData("Is the VPN down because of phishing?")]
    [InlineData("VPN VPN phishing?")]
    public async Task No_clear_policy_match_skips_the_model(string question)
    {
        using var app = _app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IPolicyModel>(new UnexpectedModel()))));
        using var client = app.CreateClient();
        using var response = await client.PostAsJsonAsync(Route, new AskRequest(question));

        response.EnsureSuccessStatusCode();
        var answer = Assert.IsType<PolicyAnswer>(await response.Content.ReadFromJsonAsync<PolicyAnswer>());
        Assert.True(answer.InsufficientInformation);
        Assert.Null(answer.Citation);
    }

    private sealed class CatalogCheckingModel : IPolicyModel
    {
        public Task<PolicyAnswer> AnswerAsync(
            string question, PolicyExcerpt excerpt, CancellationToken cancellationToken)
        {
            Assert.Equal(ZoomQuestion, question);
            Assert.Equal("IT Policy §4.2", excerpt.Citation);
            Assert.Equal(new SoftwareCatalogEntry("Zoom", IsApproved: true), excerpt.SoftwareCatalogEntry);
            return Task.FromResult(new PolicyAnswer("Zoom is approved in the software catalog.", excerpt.Citation,
                InsufficientInformation: false));
        }
    }

    private sealed class UnexpectedModel : IPolicyModel
    {
        public Task<PolicyAnswer> AnswerAsync(
            string question, PolicyExcerpt excerpt, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The model must not run for this fallback.");
    }


    private sealed class FakeModel(string? citation) : IPolicyModel
    {
        public Task<PolicyAnswer> AnswerAsync(
            string question, PolicyExcerpt excerpt, CancellationToken cancellationToken) =>
            Task.FromResult(new PolicyAnswer("Zoom is approved.", citation, InsufficientInformation: false));
    }
}
