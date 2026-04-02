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
    public void ForumSection_WithSameValues_AreEqual()
    {
        var a = new ForumSection { Url = "https://example.com/s", Name = "S", Depth = 1 };
        var b = new ForumSection { Url = "https://example.com/s", Name = "S", Depth = 1 };

        a.Should().Be(b);
    }
}
