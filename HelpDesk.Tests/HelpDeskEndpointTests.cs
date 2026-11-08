using System.Net;
using System.Net.Http.Json;
using HelpDesk.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
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

    [Fact]
    public async Task Zoom_answer_cites_the_rule_and_leaves_catalog_status_unknown()
    {
        using var client = _app.CreateClient();
        using var response = await client.PostAsJsonAsync(Route, new AskRequest(ZoomQuestion));

        response.EnsureSuccessStatusCode();
        Assert.Equal("Offline", Assert.Single(response.Headers.GetValues("X-HelpDesk-Mode")));
        var answer = Assert.IsType<PolicyAnswer>(await response.Content.ReadFromJsonAsync<PolicyAnswer>());
        Assert.True(answer.InsufficientInformation);
        Assert.Equal("IT Policy §4.2", answer.Citation);
        Assert.Contains("does not say whether Zoom", answer.Answer);
        Assert.Contains("If Zoom is listed, you may install it", answer.Answer);
        Assert.Contains("If it is not listed, get IT approval first", answer.Answer);
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

    private sealed class UnexpectedModel : IPolicyModel
    {
        public Task<PolicyAnswer> AnswerAsync(
            string question, PolicyExcerpt excerpt, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The model must not run without a policy match.");
    }


    private sealed class FakeModel(string? citation) : IPolicyModel
    {
        public Task<PolicyAnswer> AnswerAsync(
            string question, PolicyExcerpt excerpt, CancellationToken cancellationToken) =>
            Task.FromResult(new PolicyAnswer("Zoom is approved.", citation, InsufficientInformation: false));
    }
}
