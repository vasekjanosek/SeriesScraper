using FluentAssertions;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Tests.ValueObjects;

public class ForumThreadTests
{
    [Fact]
    public void ForumThread_WithOptionalPostDate_DefaultsToNull()
    {
        var thread = new ForumThread
        {
            Url = "https://forum.example.com/thread/123",
            Title = "Some Movie [1080p]"
        };

        thread.PostDate.Should().BeNull();
    }

    [Fact]
    public void ForumThread_StoresAllProperties()
    {
        var date = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var thread = new ForumThread
        {
            Url = "https://forum.example.com/thread/456",
            Title = "Breaking Bad S01",
            PostDate = date
        };

        thread.Url.Should().Be("https://forum.example.com/thread/456");
        thread.Title.Should().Be("Breaking Bad S01");
        thread.PostDate.Should().Be(date);
    }

    [Fact]
    public void ForumThread_WithSameValues_AreEqual()
    {
        var a = new ForumThread { Url = "https://example.com/t/1", Title = "Test" };
        var b = new ForumThread { Url = "https://example.com/t/1", Title = "Test" };

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void ForumThread_WithDifferentUrl_AreNotEqual()
    {
        var a = new ForumThread { Url = "https://example.com/t/1", Title = "Test" };
        var b = new ForumThread { Url = "https://example.com/t/2", Title = "Test" };

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void ForumThread_WithDifferentTitle_AreNotEqual()
    {
        var a = new ForumThread { Url = "https://example.com/t/1", Title = "A" };
        var b = new ForumThread { Url = "https://example.com/t/1", Title = "B" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void ForumThread_WithDifferentPostDate_AreNotEqual()
    {
        var a = new ForumThread
        {
            Url = "https://example.com/t/1",
            Title = "Test",
            PostDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };
        var b = new ForumThread
        {
            Url = "https://example.com/t/1",
            Title = "Test",
            PostDate = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)
        };

        a.Should().NotBe(b);
    }

    [Fact]
    public void ForumThread_GetHashCode_SameForEqualInstances()
    {
        var a = new ForumThread { Url = "https://example.com/t/1", Title = "Test" };
        var b = new ForumThread { Url = "https://example.com/t/1", Title = "Test" };

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void ForumThread_ToString_ContainsTypeName()
    {
        var thread = new ForumThread { Url = "https://example.com/t/1", Title = "Test" };

        thread.ToString().Should().Contain("ForumThread");
    }

    [Fact]
    public void ForumThread_WithExpression_CreatesModifiedCopy()
    {
        var original = new ForumThread { Url = "https://example.com/t/1", Title = "Original" };
        var modified = original with { Title = "Modified" };

        modified.Title.Should().Be("Modified");
        original.Title.Should().Be("Original");
    }
}
