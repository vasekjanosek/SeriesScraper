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
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void PostContent_WithDifferentThreadUrl_AreNotEqual()
    {
        var a = new PostContent { ThreadUrl = "https://example.com/t/1", PostIndex = 0, HtmlContent = "<p>a</p>", PlainTextContent = "a" };
        var b = new PostContent { ThreadUrl = "https://example.com/t/2", PostIndex = 0, HtmlContent = "<p>a</p>", PlainTextContent = "a" };

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void PostContent_WithDifferentPostIndex_AreNotEqual()
    {
        var a = new PostContent { ThreadUrl = "https://example.com/t/1", PostIndex = 0, HtmlContent = "<p>a</p>", PlainTextContent = "a" };
        var b = new PostContent { ThreadUrl = "https://example.com/t/1", PostIndex = 1, HtmlContent = "<p>a</p>", PlainTextContent = "a" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void PostContent_WithDifferentHtmlContent_AreNotEqual()
    {
        var a = new PostContent { ThreadUrl = "https://example.com/t/1", PostIndex = 0, HtmlContent = "<p>a</p>", PlainTextContent = "a" };
        var b = new PostContent { ThreadUrl = "https://example.com/t/1", PostIndex = 0, HtmlContent = "<p>b</p>", PlainTextContent = "a" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void PostContent_WithDifferentPlainTextContent_AreNotEqual()
    {
        var a = new PostContent { ThreadUrl = "https://example.com/t/1", PostIndex = 0, HtmlContent = "<p>a</p>", PlainTextContent = "a" };
        var b = new PostContent { ThreadUrl = "https://example.com/t/1", PostIndex = 0, HtmlContent = "<p>a</p>", PlainTextContent = "b" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void PostContent_WithDifferentPostDate_AreNotEqual()
    {
        var date1 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var a = new PostContent { ThreadUrl = "https://example.com/t/1", PostIndex = 0, HtmlContent = "<p>a</p>", PlainTextContent = "a", PostDate = date1 };
        var b = new PostContent { ThreadUrl = "https://example.com/t/1", PostIndex = 0, HtmlContent = "<p>a</p>", PlainTextContent = "a", PostDate = date2 };

        a.Should().NotBe(b);
    }

    [Fact]
    public void PostContent_GetHashCode_SameForEqualInstances()
    {
        var a = new PostContent { ThreadUrl = "https://example.com/t/1", PostIndex = 0, HtmlContent = "<p>a</p>", PlainTextContent = "a" };
        var b = new PostContent { ThreadUrl = "https://example.com/t/1", PostIndex = 0, HtmlContent = "<p>a</p>", PlainTextContent = "a" };

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void PostContent_ToString_ContainsTypeName()
    {
        var post = new PostContent { ThreadUrl = "https://example.com/t/1", PostIndex = 0, HtmlContent = "<p>a</p>", PlainTextContent = "a" };

        post.ToString().Should().Contain("PostContent");
    }

    [Fact]
    public void PostContent_WithExpression_CreatesModifiedCopy()
    {
        var original = new PostContent { ThreadUrl = "https://example.com/t/1", PostIndex = 0, HtmlContent = "<p>orig</p>", PlainTextContent = "orig" };
        var modified = original with { PostIndex = 5 };

        modified.PostIndex.Should().Be(5);
        original.PostIndex.Should().Be(0);
    }
}
