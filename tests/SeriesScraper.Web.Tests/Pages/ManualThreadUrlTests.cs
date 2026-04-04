using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Web.Pages;

namespace SeriesScraper.Web.Tests.Pages;

/// <summary>
/// bUnit tests for Results.razor ScrapeThreadByUrl() method (AC5 of issue #104).
/// Covers the 6 scenarios required for ≥90% coverage on that path.
/// </summary>
public class ManualThreadUrlTests : TestContext
{
    private readonly IScrapeRunService _scrapeRunService;
    private readonly IForumCrudService _forumCrudService;
    private readonly IResultsService _resultsService;

    private static readonly IReadOnlyList<ForumDto> TestForums =
    [
        new ForumDto
        {
            ForumId = 1,
            Name = "Test Forum",
            BaseUrl = "http://forum.example.com",
            Username = "user",
            CredentialKey = "key"
        }
    ];

    private static readonly PagedResult<ResultSummaryDto> EmptyPaged = new()
    {
        Items = [],
        TotalCount = 0,
        Page = 1,
        PageSize = 20
    };

    public ManualThreadUrlTests()
    {
        _scrapeRunService = Substitute.For<IScrapeRunService>();
        _forumCrudService = Substitute.For<IForumCrudService>();
        _resultsService = Substitute.For<IResultsService>();

        _forumCrudService
            .GetAllForumsAsync(Arg.Any<CancellationToken>())
            .Returns(TestForums);

        _resultsService
            .GetResultsAsync(
                Arg.Any<ResultFilterDto>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPaged);

        Services.AddSingleton(_scrapeRunService);
        Services.AddSingleton(_forumCrudService);
        Services.AddSingleton(_resultsService);
    }

    // Scenario 1: no forum selected → error shown
    [Fact]
    public void ScrapeThread_NoForumSelected_ShowsForumError()
    {
        var cut = RenderComponent<Results>();

        // Forum select defaults to 0 — click Scrape without selecting
        cut.Find("button.btn-success").Click();

        cut.Find(".alert-danger").TextContent.Should().Contain("Please select a forum.");
    }

    // Scenario 2: empty URL → error shown
    [Fact]
    public void ScrapeThread_EmptyUrl_ShowsUrlError()
    {
        var cut = RenderComponent<Results>();

        // Select a valid forum (forum select is the first select on the page)
        cut.FindAll("select")[0].Change("1");

        // URL field left empty — click Scrape
        cut.Find("button.btn-success").Click();

        cut.Find(".alert-danger").TextContent.Should().Contain("Please enter a thread URL.");
    }

    // Scenario 3: ArgumentException from service → displayed as error, no crash
    [Fact]
    public async Task ScrapeThread_ServiceThrowsArgumentException_ShowsErrorMessage()
    {
        _scrapeRunService
            .ScrapeByUrlAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ScrapeRun>(new ArgumentException("Invalid forum URL format.")));

        var cut = RenderComponent<Results>();

        cut.FindAll("select")[0].Change("1");
        cut.Find("input[type=url]").Change("http://forum.example.com/viewtopic.php?t=1");

        await cut.InvokeAsync(() => cut.Find("button.btn-success").Click());

        cut.Find(".alert-danger").TextContent.Should().Contain("Invalid forum URL format.");
    }

    // Scenario 4: InvalidOperationException from service → displayed as error
    [Fact]
    public async Task ScrapeThread_ServiceThrowsInvalidOperationException_ShowsErrorMessage()
    {
        _scrapeRunService
            .ScrapeByUrlAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ScrapeRun>(new InvalidOperationException("Scraper is not available.")));

        var cut = RenderComponent<Results>();

        cut.FindAll("select")[0].Change("1");
        cut.Find("input[type=url]").Change("http://forum.example.com/viewtopic.php?t=2");

        await cut.InvokeAsync(() => cut.Find("button.btn-success").Click());

        cut.Find(".alert-danger").TextContent.Should().Contain("Scraper is not available.");
    }

    // Scenario 5: valid submission → success message shown, URL field cleared
    [Fact]
    public async Task ScrapeThread_ValidSubmission_ShowsSuccessAndClearsUrl()
    {
        _scrapeRunService
            .ScrapeByUrlAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ScrapeRun { RunId = 42 });

        var cut = RenderComponent<Results>();

        cut.FindAll("select")[0].Change("1");
        cut.Find("input[type=url]").Change("http://forum.example.com/viewtopic.php?t=99");

        await cut.InvokeAsync(() => cut.Find("button.btn-success").Click());

        cut.Find(".alert-success").TextContent.Should().Contain("Scrape run #42 started.");
        (cut.Find("input[type=url]").GetAttribute("value") ?? string.Empty).Should().BeEmpty();
    }

    // Scenario 6: button disabled while submitting
    [Fact]
    public async Task ScrapeThread_WhileSubmitting_ButtonIsDisabled()
    {
        var tcs = new TaskCompletionSource<ScrapeRun>();
        _scrapeRunService
            .ScrapeByUrlAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        var cut = RenderComponent<Results>();

        cut.FindAll("select")[0].Change("1");
        cut.Find("input[type=url]").Change("http://forum.example.com/viewtopic.php?t=99");

        // Fire click but do not await — the handler suspends at ScrapeByUrlAsync (TCS)
        // leaving _scrapeSubmitting = true
        _ = cut.InvokeAsync(() => cut.Find("button.btn-success").Click());

        cut.WaitForAssertion(
            () => cut.Find("button.btn-success").HasAttribute("disabled").Should().BeTrue(),
            timeout: TimeSpan.FromSeconds(2));

        // Cleanup: cancel the pending task so no background work leaks
        tcs.SetCanceled();
        await Task.Delay(50);
    }
}
