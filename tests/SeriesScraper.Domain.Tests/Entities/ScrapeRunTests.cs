using FluentAssertions;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;

namespace SeriesScraper.Domain.Tests.Entities;

public class ScrapeRunTests
{
    [Fact]
    public void ScrapeRun_DefaultStatus_IsPending()
    {
        var run = new ScrapeRun();

        run.Status.Should().Be(ScrapeRunStatus.Pending);
    }

    [Fact]
    public void ScrapeRun_Items_InitializesEmpty()
    {
        var run = new ScrapeRun();

        run.Items.Should().NotBeNull();
        run.Items.Should().BeEmpty();
    }

    [Fact]
    public void ScrapeRun_CompletedAt_DefaultsToNull()
    {
        var run = new ScrapeRun();

        run.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void ScrapeRun_CanAddItems()
    {
        var run = new ScrapeRun();
        var item = new ScrapeRunItem { PostUrl = "http://forum.com/thread/1" };

        run.Items.Add(item);

        run.Items.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(ScrapeRunStatus.Pending)]
    [InlineData(ScrapeRunStatus.Running)]
    [InlineData(ScrapeRunStatus.Complete)]
    [InlineData(ScrapeRunStatus.Failed)]
    [InlineData(ScrapeRunStatus.Partial)]
    public void ScrapeRun_AllStatusTransitions(ScrapeRunStatus status)
    {
        var run = new ScrapeRun { Status = status };

        run.Status.Should().Be(status);
    }
}
