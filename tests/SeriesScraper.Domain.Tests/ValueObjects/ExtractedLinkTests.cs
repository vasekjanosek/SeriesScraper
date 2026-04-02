using FluentAssertions;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Tests.ValueObjects;

public class ExtractedLinkTests
{
    [Fact]
    public void ExtractedLink_StoresRequiredProperties()
    {
        var link = new ExtractedLink
        {
            Url = "https://download.example.com/file.zip",
            Scheme = "https"
        };

        link.Url.Should().Be("https://download.example.com/file.zip");
        link.Scheme.Should().Be("https");
        link.LinkText.Should().BeNull();
    }

    [Fact]
    public void ExtractedLink_WithLinkText_StoresText()
    {
        var link = new ExtractedLink
        {
            Url = "https://download.example.com/file.zip",
            Scheme = "https",
            LinkText = "Download File"
        };

        link.LinkText.Should().Be("Download File");
    }

    [Fact]
    public void ExtractedLink_WithSameValues_AreEqual()
    {
        var a = new ExtractedLink { Url = "https://x.com/f", Scheme = "https" };
        var b = new ExtractedLink { Url = "https://x.com/f", Scheme = "https" };

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void ExtractedLink_WithDifferentUrl_AreNotEqual()
    {
        var a = new ExtractedLink { Url = "https://x.com/a", Scheme = "https" };
        var b = new ExtractedLink { Url = "https://x.com/b", Scheme = "https" };

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void ExtractedLink_WithDifferentScheme_AreNotEqual()
    {
        var a = new ExtractedLink { Url = "magnet:?xt=abc", Scheme = "magnet" };
        var b = new ExtractedLink { Url = "magnet:?xt=abc", Scheme = "https" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void ExtractedLink_WithDifferentLinkText_AreNotEqual()
    {
        var a = new ExtractedLink { Url = "https://x.com/f", Scheme = "https", LinkText = "A" };
        var b = new ExtractedLink { Url = "https://x.com/f", Scheme = "https", LinkText = "B" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void ExtractedLink_GetHashCode_SameForEqualInstances()
    {
        var a = new ExtractedLink { Url = "https://x.com/f", Scheme = "https", LinkText = "DL" };
        var b = new ExtractedLink { Url = "https://x.com/f", Scheme = "https", LinkText = "DL" };

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void ExtractedLink_ToString_ContainsTypeName()
    {
        var link = new ExtractedLink { Url = "https://x.com/f", Scheme = "https" };

        link.ToString().Should().Contain("ExtractedLink");
    }

    [Fact]
    public void ExtractedLink_WithExpression_CreatesModifiedCopy()
    {
        var original = new ExtractedLink { Url = "https://x.com/f", Scheme = "https" };
        var modified = original with { LinkText = "New Text" };

        modified.LinkText.Should().Be("New Text");
        original.LinkText.Should().BeNull();
    }
}
