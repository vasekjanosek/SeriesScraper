using FluentAssertions;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Tests.ValueObjects;

public class ForumSectionTests
{
    [Fact]
    public void ForumSection_TopLevel_HasNullParentUrl()
    {
        var section = new ForumSection
        {
            Url = "https://forum.example.com/section1",
            Name = "Movies",
            Depth = 1
        };

        section.ParentUrl.Should().BeNull();
        section.Depth.Should().Be(1);
    }

    [Fact]
    public void ForumSection_SubSection_HasParentUrl()
    {
        var section = new ForumSection
        {
            Url = "https://forum.example.com/section1/sub1",
            Name = "Action Movies",
            ParentUrl = "https://forum.example.com/section1",
            Depth = 2
        };

        section.ParentUrl.Should().Be("https://forum.example.com/section1");
        section.Depth.Should().Be(2);
    }

    [Fact]
    public void ForumSection_StoresAllProperties()
    {
        var section = new ForumSection
        {
            Url = "https://forum.example.com/s/1",
            Name = "TV Series",
            ParentUrl = "https://forum.example.com/root",
            Depth = 3
        };

        section.Url.Should().Be("https://forum.example.com/s/1");
        section.Name.Should().Be("TV Series");
        section.ParentUrl.Should().Be("https://forum.example.com/root");
        section.Depth.Should().Be(3);
    }

    [Fact]
    public void ForumSection_WithSameValues_AreEqual()
    {
        var a = new ForumSection { Url = "https://example.com/s", Name = "S", Depth = 1 };
        var b = new ForumSection { Url = "https://example.com/s", Name = "S", Depth = 1 };

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void ForumSection_WithDifferentUrl_AreNotEqual()
    {
        var a = new ForumSection { Url = "https://example.com/s1", Name = "S", Depth = 1 };
        var b = new ForumSection { Url = "https://example.com/s2", Name = "S", Depth = 1 };

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void ForumSection_WithDifferentName_AreNotEqual()
    {
        var a = new ForumSection { Url = "https://example.com/s", Name = "A", Depth = 1 };
        var b = new ForumSection { Url = "https://example.com/s", Name = "B", Depth = 1 };

        a.Should().NotBe(b);
    }

    [Fact]
    public void ForumSection_WithDifferentDepth_AreNotEqual()
    {
        var a = new ForumSection { Url = "https://example.com/s", Name = "S", Depth = 1 };
        var b = new ForumSection { Url = "https://example.com/s", Name = "S", Depth = 2 };

        a.Should().NotBe(b);
    }

    [Fact]
    public void ForumSection_WithDifferentParentUrl_AreNotEqual()
    {
        var a = new ForumSection { Url = "https://example.com/s", Name = "S", Depth = 2, ParentUrl = "https://example.com/p1" };
        var b = new ForumSection { Url = "https://example.com/s", Name = "S", Depth = 2, ParentUrl = "https://example.com/p2" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void ForumSection_GetHashCode_SameForEqualInstances()
    {
        var a = new ForumSection { Url = "https://example.com/s", Name = "S", Depth = 1 };
        var b = new ForumSection { Url = "https://example.com/s", Name = "S", Depth = 1 };

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void ForumSection_ToString_ContainsTypeName()
    {
        var section = new ForumSection { Url = "https://example.com/s", Name = "S", Depth = 1 };

        section.ToString().Should().Contain("ForumSection");
    }

    [Fact]
    public void ForumSection_WithExpression_CreatesModifiedCopy()
    {
        var original = new ForumSection { Url = "https://example.com/s", Name = "Original", Depth = 1 };
        var modified = original with { Name = "Modified", Depth = 2 };

        modified.Name.Should().Be("Modified");
        modified.Depth.Should().Be(2);
        original.Name.Should().Be("Original");
    }
}
