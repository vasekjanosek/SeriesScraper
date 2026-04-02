using FluentAssertions;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;

namespace SeriesScraper.Domain.Tests.Entities;

public class ScrapeRunItemTests
{
    [Fact]
    public void ScrapeRunItem_DefaultStatus_IsPending()
    {
        var item = new ScrapeRunItem { PostUrl = "http://forum.com/thread/1" };

        item.Status.Should().Be(ScrapeRunItemStatus.Pending);
    }

    [Fact]
    public void ScrapeRunItem_ItemId_DefaultsToNull()
    {
        var item = new ScrapeRunItem { PostUrl = "http://forum.com/thread/1" };

        item.ItemId.Should().BeNull();
    }

    [Fact]
    public void ScrapeRunItem_ProcessedAt_DefaultsToNull()
    {
        var item = new ScrapeRunItem { PostUrl = "http://forum.com/thread/1" };

        item.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public void ScrapeRunItem_CanSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var item = new ScrapeRunItem
        {
            RunItemId = 1,
            RunId = 10,
            PostUrl = "http://forum.com/thread/42",
            ItemId = 99,
            Status = ScrapeRunItemStatus.Done,
            ProcessedAt = now
        };

        item.RunItemId.Should().Be(1);
        item.RunId.Should().Be(10);
        item.PostUrl.Should().Be("http://forum.com/thread/42");
        item.ItemId.Should().Be(99);
        item.Status.Should().Be(ScrapeRunItemStatus.Done);
        item.ProcessedAt.Should().Be(now);
    }

    [Theory]
    [InlineData(ScrapeRunItemStatus.Pending)]
    [InlineData(ScrapeRunItemStatus.Processing)]
    [InlineData(ScrapeRunItemStatus.Done)]
    [InlineData(ScrapeRunItemStatus.Failed)]
    [InlineData(ScrapeRunItemStatus.Skipped)]
    public void ScrapeRunItem_StatusCanBeSetToAnyValue(ScrapeRunItemStatus status)
    {
        var item = new ScrapeRunItem { PostUrl = "http://forum.com/thread/1" };

        item.Status = status;

        item.Status.Should().Be(status);
    }
}
