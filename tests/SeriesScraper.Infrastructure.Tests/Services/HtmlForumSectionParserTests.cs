using FluentAssertions;
using SeriesScraper.Infrastructure.Services;

namespace SeriesScraper.Infrastructure.Tests.Services;

public class HtmlForumSectionParserTests
{
    private readonly HtmlForumSectionParser _sut = new();
    private const string BaseUrl = "https://forum.example.com";

    // ── phpBB2 Pattern ─────────────────────────────────────────────────────

    [Fact]
    public void ParseSections_PhpBB2Html_ExtractsSections()
    {
        var html = @"
        <html><body><table>
        <tr>
          <td class='cat' colspan='2'>
            <a href='index.php?c=23&amp;sid=abc' class='cattitle'>Movie Hall</a>
          </td>
        </tr>
        <tr>
          <td class='row1' height='45'>
            <img src='templates/subSilver/images/folder.gif' class='imgfolder'>
          </td>
          <td class='row1' width='100%'>
            <a href='viewforum.php?f=324&amp;sid=abc' class='nav'>HD - Serialy</a>
            <span class='genmed'>HD series downloads</span>
          </td>
          <td class='row2' align='center'>
            <span class='gensmall'>4051</span>
          </td>
        </tr>
        <tr>
          <td class='row1' height='45'>
            <img src='templates/subSilver/images/folder.gif' class='imgfolder'>
          </td>
          <td class='row1' width='100%'>
            <a href='viewforum.php?f=325&amp;sid=abc' class='nav'>SD - Serialy</a>
            <span class='genmed'>SD series downloads</span>
          </td>
          <td class='row2' align='center'>
            <span class='gensmall'>1200</span>
          </td>
        </tr>
        </table></body></html>";

        var result = _sut.ParseSections(html, BaseUrl);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("HD - Serialy");
        result[0].Url.Should().Be("https://forum.example.com/viewforum.php?f=324&sid=abc");
        result[0].Depth.Should().Be(1);
        result[1].Name.Should().Be("SD - Serialy");
        result[1].Url.Should().Be("https://forum.example.com/viewforum.php?f=325&sid=abc");
    }

    [Fact]
    public void ParseSections_PhpBB2Html_FiltersBreadcrumbs()
    {
        // Breadcrumb nav links point to index.php, not viewforum.php
        var html = @"
        <html><body>
        <a href='index.php' class='nav'>Home</a>
        <a href='index.php?c=23' class='nav'>Movie Hall</a>
        <table>
        <tr>
          <td class='row1' width='100%'>
            <a href='viewforum.php?f=324&amp;sid=abc' class='nav'>HD - Serialy</a>
          </td>
        </tr>
        </table>
        </body></html>";

        var result = _sut.ParseSections(html, BaseUrl);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("HD - Serialy");
        result[0].Url.Should().Contain("viewforum.php?f=324");
    }

    [Fact]
    public void ParseSections_PhpBB2Html_ExtractsCategory()
    {
        var html = @"
        <html><body><table>
        <tr>
          <td class='cat' colspan='2'>
            <a href='index.php?c=23' class='cattitle'>Movie Hall</a>
          </td>
        </tr>
        <tr>
          <td class='row1' width='100%'>
            <a href='viewforum.php?f=324' class='nav'>HD - Serialy</a>
          </td>
        </tr>
        <tr>
          <td class='cat' colspan='2'>
            <a href='index.php?c=24' class='cattitle'>Music Section</a>
          </td>
        </tr>
        <tr>
          <td class='row1' width='100%'>
            <a href='viewforum.php?f=400' class='nav'>MP3 Albums</a>
          </td>
        </tr>
        </table></body></html>";

        var result = _sut.ParseSections(html, BaseUrl);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("HD - Serialy");
        result[1].Name.Should().Be("MP3 Albums");
    }

    [Fact]
    public void ParseSections_PhpBB2Html_DeduplicatesSameUrl()
    {
        var html = @"
        <html><body>
        <a href='viewforum.php?f=324' class='nav'>HD - Serialy</a>
        <a href='viewforum.php?f=324' class='nav'>HD - Serialy</a>
        </body></html>";

        var result = _sut.ParseSections(html, BaseUrl);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void ParseSections_PhpBB2Html_ResolvesRelativeUrls()
    {
        var html = @"
        <html><body>
        <a href='viewforum.php?f=100' class='nav'>Section</a>
        </body></html>";

        var result = _sut.ParseSections(html, "https://warforum.xyz/index.php");

        result.Should().HaveCount(1);
        result[0].Url.Should().Be("https://warforum.xyz/viewforum.php?f=100");
    }

    [Fact]
    public void ParseSections_PhpBB2NotTriggeredByPhpBB3()
    {
        // phpBB3 uses class='forumtitle', not class='nav' — should NOT match phpBB2 parser
        var html = @"
        <html><body>
            <a href='./viewforum.php?f=1' class='forumtitle'>Movies</a>
        </body></html>";

        var result = _sut.ParseSections(html, BaseUrl);

        // Should be parsed by phpBB3, not phpBB2 — result is still valid
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Movies");
    }

    // ── phpBB3 Pattern ─────────────────────────────────────────────────────

    [Fact]
    public void ParseSections_PhpBBHtml_ExtractsSections()
    {
        var html = @"
        <html><body>
        <div class='forabg'>
            <ul class='topiclist forums'>
                <li class='row'>
                    <dl class='icon'>
                        <dt><div class='list-inner'>
                            <a href='./viewforum.php?f=1' class='forumtitle'>Movies</a>
                            <br />Download movies here
                        </div></dt>
                    </dl>
                </li>
                <li class='row'>
                    <dl class='icon'>
                        <dt><div class='list-inner'>
                            <a href='./viewforum.php?f=2' class='forumtitle'>TV Series</a>
                            <br />TV show downloads
                        </div></dt>
                    </dl>
                </li>
            </ul>
        </div>
        </body></html>";

        var result = _sut.ParseSections(html, BaseUrl);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Movies");
        result[0].Url.Should().Be("https://forum.example.com/viewforum.php?f=1");
        result[1].Name.Should().Be("TV Series");
        result[1].Url.Should().Be("https://forum.example.com/viewforum.php?f=2");
    }

    // ── vBulletin Pattern ──────────────────────────────────────────────────

    [Fact]
    public void ParseSections_VBulletinHtml_ExtractsSections()
    {
        var html = @"
        <html><body>
        <table class='tborder' id='forums'>
            <tbody>
                <tr>
                    <td class='alt1Active' id='f1'>
                        <a href='forumdisplay.php?f=1'><strong>Movies</strong></a>
                        <div class='smallfont'>Movie downloads</div>
                    </td>
                </tr>
                <tr>
                    <td class='alt1Active' id='f2'>
                        <a href='forumdisplay.php?f=2'><strong>TV Series</strong></a>
                        <div class='smallfont'>TV show downloads</div>
                    </td>
                </tr>
            </tbody>
        </table>
        </body></html>";

        var result = _sut.ParseSections(html, BaseUrl);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Movies");
        result[0].Url.Should().Contain("forumdisplay.php?f=1");
        result[1].Name.Should().Be("TV Series");
        result[1].Url.Should().Contain("forumdisplay.php?f=2");
    }

    // ── XenForo Pattern ────────────────────────────────────────────────────

    [Fact]
    public void ParseSections_XenForoHtml_ExtractsSections()
    {
        var html = @"
        <html><body>
        <div class='node node--forum' data-node-id='1'>
            <div class='node-body'>
                <h3 class='node-title'>
                    <a href='/forums/movies.1/'>Movies</a>
                </h3>
            </div>
        </div>
        <div class='node node--forum' data-node-id='2'>
            <div class='node-body'>
                <h3 class='node-title'>
                    <a href='/forums/tv-series.2/'>TV Series</a>
                </h3>
            </div>
        </div>
        </body></html>";

        var result = _sut.ParseSections(html, BaseUrl);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Movies");
        result[0].Url.Should().Be("https://forum.example.com/forums/movies.1/");
        result[1].Name.Should().Be("TV Series");
        result[1].Url.Should().Be("https://forum.example.com/forums/tv-series.2/");
    }

    // ── Generic Pattern ────────────────────────────────────────────────────

    [Fact]
    public void ParseSections_GenericForumLinks_ExtractsSections()
    {
        var html = @"
        <html><body>
        <nav>
            <a href='/forum/movies'>Movies</a>
            <a href='/forum/tv-series'>TV Series</a>
            <a href='/about'>About</a>
        </nav>
        </body></html>";

        var result = _sut.ParseSections(html, BaseUrl);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Movies");
        result[1].Name.Should().Be("TV Series");
    }

    // ── Edge Cases ─────────────────────────────────────────────────────────

    [Fact]
    public void ParseSections_EmptyHtml_ReturnsEmpty()
    {
        var result = _sut.ParseSections("<html><body></body></html>", BaseUrl);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseSections_NullHtml_ThrowsArgumentNullException()
    {
        var act = () => _sut.ParseSections(null!, BaseUrl);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseSections_NullBaseUrl_ThrowsArgumentNullException()
    {
        var act = () => _sut.ParseSections("<html></html>", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseSections_ResolvesRelativeUrls()
    {
        var html = @"
        <html><body>
            <a href='./viewforum.php?f=5' class='forumtitle'>Section</a>
        </body></html>";

        var result = _sut.ParseSections(html, "https://forum.example.com/index.php");

        result.Should().HaveCount(1);
        result[0].Url.Should().Be("https://forum.example.com/viewforum.php?f=5");
    }

    [Fact]
    public void ParseSections_DeEntitizesNames()
    {
        var html = @"
        <html><body>
            <a href='./viewforum.php?f=1' class='forumtitle'>Movies &amp; TV</a>
        </body></html>";

        var result = _sut.ParseSections(html, BaseUrl);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Movies & TV");
    }

    [Fact]
    public void ParseSections_SkipsDuplicateUrls_VBulletin()
    {
        var html = @"
        <html><body>
            <a href='forumdisplay.php?f=1'>Movies</a>
            <a href='forumdisplay.php?f=1'>Movies</a>
        </body></html>";

        var result = _sut.ParseSections(html, BaseUrl);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void ParseSections_SkipsEmptyNameLinks()
    {
        var html = @"
        <html><body>
            <a href='./viewforum.php?f=1' class='forumtitle'>  </a>
            <a href='./viewforum.php?f=2' class='forumtitle'>Valid</a>
        </body></html>";

        var result = _sut.ParseSections(html, BaseUrl);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Valid");
    }

    [Fact]
    public void ParseSections_AllSectionsHaveDepthOne()
    {
        var html = @"
        <html><body>
            <a href='./viewforum.php?f=1' class='forumtitle'>Section A</a>
            <a href='./viewforum.php?f=2' class='forumtitle'>Section B</a>
        </body></html>";

        var result = _sut.ParseSections(html, BaseUrl);

        result.Should().AllSatisfy(s => s.Depth.Should().Be(1));
    }

    [Fact]
    public void ParseSections_AbsoluteUrlsPreserved()
    {
        var html = @"
        <html><body>
            <a href='https://other.example.com/viewforum.php?f=1' class='forumtitle'>External</a>
        </body></html>";

        var result = _sut.ParseSections(html, BaseUrl);

        result.Should().HaveCount(1);
        result[0].Url.Should().Be("https://other.example.com/viewforum.php?f=1");
    }

    [Fact]
    public void ParseSections_NoLinks_ReturnsEmpty()
    {
        var html = @"<html><body><p>No forums here</p></body></html>";

        var result = _sut.ParseSections(html, BaseUrl);

        result.Should().BeEmpty();
    }

    // ── ResolveUrl ─────────────────────────────────────────────────────────

    [Fact]
    public void ResolveUrl_AbsoluteUrl_ReturnsAsIs()
    {
        var result = HtmlForumSectionParser.ResolveUrl(
            "https://forum.example.com", "https://other.com/page");

        result.Should().Be("https://other.com/page");
    }

    [Fact]
    public void ResolveUrl_RelativePath_ResolvesAgainstBase()
    {
        var result = HtmlForumSectionParser.ResolveUrl(
            "https://forum.example.com", "viewforum.php?f=1");

        result.Should().Be("https://forum.example.com/viewforum.php?f=1");
    }

    [Fact]
    public void ResolveUrl_DotSlashPrefix_Resolves()
    {
        var result = HtmlForumSectionParser.ResolveUrl(
            "https://forum.example.com", "./viewforum.php?f=1");

        result.Should().Be("https://forum.example.com/viewforum.php?f=1");
    }

    // ── IsForumSectionUrl ──────────────────────────────────────────────────

    [Theory]
    [InlineData("forumdisplay.php?f=1", true)]
    [InlineData("viewforum.php?f=1", true)]
    [InlineData("/forums/movies.1/", true)]
    [InlineData("/board/section", true)]
    [InlineData("/about", false)]
    [InlineData("/contact", false)]
    [InlineData("javascript:void(0)", false)]
    public void IsForumSectionUrl_DetectsForumPatterns(string href, bool expected)
    {
        HtmlForumSectionParser.IsForumSectionUrl(href).Should().Be(expected);
    }
}
