using FluentAssertions;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Tests.ValueObjects;

public class PostContentTests
{
    [Fact]
    public void PostContent_StoresAllRequiredProperties()
    {
        var post = new PostContent
        {
            ThreadUrl = "https://forum.example.com/thread/123",
            PostIndex = 0,
            HtmlContent = "<p>Download here</p>",
            PlainTextContent = "Download here"
        };

        post.ThreadUrl.Should().Be("https://forum.example.com/thread/123");
        post.PostIndex.Should().Be(0);
        post.HtmlContent.Should().Be("<p>Download here</p>");
        post.PlainTextContent.Should().Be("Download here");
        post.PostDate.Should().BeNull();
    }

    [Fact]
    public void PostContent_WithPostDate_StoresDate()
    {
        var date = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var post = new PostContent
        {
            ThreadUrl = "https://forum.example.com/thread/1",
            PostIndex = 2,
            HtmlContent = "<p>text</p>",
            PlainTextContent = "text",
            PostDate = date
        };

        post.PostDate.Should().Be(date);
    }

    [Fact]
    public void PostContent_WithSameValues_AreEqual()
    {
        var a = new PostContent
        {
            ThreadUrl = "https://example.com/t/1",
            PostIndex = 0,
            HtmlContent = "<p>a</p>",
            PlainTextContent = "a"
        };
        var b = new PostContent
        {
            ThreadUrl = "https://example.com/t/1",
            PostIndex = 0,
            HtmlContent = "<p>a</p>",
            PlainTextContent = "a"
        };

        a.Should().Be(b);
    }
}
