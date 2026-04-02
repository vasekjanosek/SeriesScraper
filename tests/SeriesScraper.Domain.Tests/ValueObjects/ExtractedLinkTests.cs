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
    }
}
