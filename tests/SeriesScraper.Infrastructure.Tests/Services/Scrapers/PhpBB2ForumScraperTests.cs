using System.Net;
using FluentAssertions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SeriesScraper.Domain.Exceptions;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;
using SeriesScraper.Infrastructure.Services.Scrapers;

namespace SeriesScraper.Infrastructure.Tests.Services.Scrapers;

public class PhpBB2ForumScraperTests : IDisposable
{
    private readonly IHtmlForumSectionParser _sectionParser;
    private readonly IResponseValidator _responseValidator;
    private readonly ILogger<PhpBB2ForumScraper> _logger;
    private readonly CookieContainer _cookieContainer;

    private TestHttpMessageHandler _httpHandler = null!;
    private HttpClient _httpClient = null!;
    private PhpBB2ForumScraper _sut = null!;

    private static readonly ForumCredentials ValidCredentials = new()
    {
        Username = "testuser",
        Password = "testpass",
        BaseUrl = "https://forum.example.com"
    };

    public PhpBB2ForumScraperTests()
    {
        _sectionParser = Substitute.For<IHtmlForumSectionParser>();
        _responseValidator = Substitute.For<IResponseValidator>();
        _logger = Substitute.For<ILogger<PhpBB2ForumScraper>>();
        _cookieContainer = new CookieContainer();

        // Default: responses are NOT expired
        _responseValidator.IsSessionExpired(Arg.Any<string>()).Returns(false);

        CreateSut("<html><body>Forum Home</body></html>");
    }

    private void CreateSut(string defaultResponse, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _httpHandler?.Dispose();
        _httpClient?.Dispose();

        _httpHandler = new TestHttpMessageHandler(defaultResponse, statusCode);
        _httpClient = new HttpClient(_httpHandler)
        {
            BaseAddress = new Uri("https://forum.example.com")
        };
        _sut = new PhpBB2ForumScraper(_sectionParser, _responseValidator, _logger, _httpClient, _cookieContainer);
    }

    public void Dispose()
    {
        _sut?.Dispose();
        _httpClient?.Dispose();
        _httpHandler?.Dispose();
    }

    // ===== Constructor Validation =====

    [Fact]
    public void Constructor_NullSectionParser_Throws()
    {
        var act = () => new PhpBB2ForumScraper(null!, _responseValidator, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("sectionParser");
    }

    [Fact]
    public void Constructor_NullResponseValidator_Throws()
    {
        var act = () => new PhpBB2ForumScraper(_sectionParser, null!, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("responseValidator");
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var act = () => new PhpBB2ForumScraper(_sectionParser, _responseValidator, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void InternalConstructor_NullHttpClient_Throws()
    {
        var act = () => new PhpBB2ForumScraper(_sectionParser, _responseValidator, _logger, null!, _cookieContainer);
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpClient");
    }

    [Fact]
    public void InternalConstructor_NullCookieContainer_Throws()
    {
        using var client = new HttpClient();
        var act = () => new PhpBB2ForumScraper(_sectionParser, _responseValidator, _logger, client, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("cookieContainer");
    }

    // ===== AuthenticateAsync =====

    [Fact]
    public async Task AuthenticateAsync_NullCredentials_Throws()
    {
        var act = () => _sut.AuthenticateAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AuthenticateAsync_NullBaseUrl_ThrowsScrapingException()
    {
        var creds = new ForumCredentials { Username = "u", Password = "p", BaseUrl = null };
        var act = () => _sut.AuthenticateAsync(creds);
        await act.Should().ThrowAsync<ScrapingException>().WithMessage("*BaseUrl*");
    }

    [Fact]
    public async Task AuthenticateAsync_EmptyBaseUrl_ThrowsScrapingException()
    {
        var creds = new ForumCredentials { Username = "u", Password = "p", BaseUrl = "   " };
        var act = () => _sut.AuthenticateAsync(creds);
        await act.Should().ThrowAsync<ScrapingException>().WithMessage("*BaseUrl*");
    }

    [Fact]
    public async Task AuthenticateAsync_SuccessfulLogin_ReturnsTrue()
    {
        CreateSut("<html><body>Welcome back testuser</body></html>");
        _responseValidator.IsSessionExpired(Arg.Any<string>()).Returns(false);

        var result = await _sut.AuthenticateAsync(ValidCredentials);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticateAsync_PostsToLoginPhp()
    {
        CreateSut("<html><body>Welcome</body></html>");
        _responseValidator.IsSessionExpired(Arg.Any<string>()).Returns(false);

        await _sut.AuthenticateAsync(ValidCredentials);

        _httpHandler.LastRequestUri.Should().NotBeNull();
        _httpHandler.LastRequestUri!.AbsoluteUri.Should().Be("https://forum.example.com/login.php");
        _httpHandler.LastRequestMethod.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task AuthenticateAsync_SendsCorrectFormData()
    {
        CreateSut("<html><body>Welcome</body></html>");
        _responseValidator.IsSessionExpired(Arg.Any<string>()).Returns(false);

        await _sut.AuthenticateAsync(ValidCredentials);

        _httpHandler.LastRequestContent.Should().Contain("username=testuser");
        _httpHandler.LastRequestContent.Should().Contain("password=testpass");
        _httpHandler.LastRequestContent.Should().Contain("login=Log+in");
        _httpHandler.LastRequestContent.Should().Contain("autologin=on");
    }

    [Fact]
    public async Task AuthenticateAsync_LoginPageReturned_ReturnsFalse()
    {
        CreateSut("<html><form action='login.php'><input name='login'/></form></html>");
        _responseValidator.IsSessionExpired(Arg.Any<string>()).Returns(true);

        var result = await _sut.AuthenticateAsync(ValidCredentials);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AuthenticateAsync_HttpError_ThrowsScrapingException()
    {
        CreateSut("", HttpStatusCode.InternalServerError);

        var act = () => _sut.AuthenticateAsync(ValidCredentials);

        await act.Should().ThrowAsync<ScrapingException>().WithMessage("*Network error*");
    }

    [Fact]
    public async Task AuthenticateAsync_TrailingSlashInBaseUrl_HandledCorrectly()
    {
        CreateSut("<html><body>Welcome</body></html>");
        _responseValidator.IsSessionExpired(Arg.Any<string>()).Returns(false);

        var creds = ValidCredentials with { BaseUrl = "https://forum.example.com/" };
        await _sut.AuthenticateAsync(creds);

        _httpHandler.LastRequestUri!.AbsoluteUri.Should().Be("https://forum.example.com/login.php");
    }

    // ===== GetCookieContainer =====

    [Fact]
    public void GetCookieContainer_ReturnsCookieContainer()
    {
        var container = _sut.GetCookieContainer();
        container.Should().BeSameAs(_cookieContainer);
    }

    // ===== ValidateSessionAsync =====

    [Fact]
    public async Task ValidateSessionAsync_NoAuthentication_ReturnsFalse()
    {
        var result = await _sut.ValidateSessionAsync();
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSessionAsync_AfterAuth_ValidSession_ReturnsTrue()
    {
        CreateSut("<html><body>Forum content</body></html>");
        _responseValidator.IsSessionExpired(Arg.Any<string>()).Returns(false);

        await _sut.AuthenticateAsync(ValidCredentials);
        var result = await _sut.ValidateSessionAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateSessionAsync_AfterAuth_ExpiredSession_ReturnsFalse()
    {
        CreateSut("<html><body>Content</body></html>");
        // First call (auth) returns false (not expired), second call (validate) returns true (expired)
        _responseValidator.IsSessionExpired(Arg.Any<string>())
            .Returns(false, true);

        await _sut.AuthenticateAsync(ValidCredentials);
        var result = await _sut.ValidateSessionAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSessionAsync_HttpError_ReturnsFalse()
    {
        // First authenticate successfully
        CreateSut("<html><body>Welcome</body></html>");
        _responseValidator.IsSessionExpired(Arg.Any<string>()).Returns(false);
        await _sut.AuthenticateAsync(ValidCredentials);

        // Then make next request fail
        _httpHandler.SetNextResponse("", HttpStatusCode.InternalServerError);

        var result = await _sut.ValidateSessionAsync();
        result.Should().BeFalse();
    }

    // ===== EnumerateSectionsAsync =====

    [Fact]
    public async Task EnumerateSectionsAsync_NullBaseUrl_Throws()
    {
        var act = async () =>
        {
            await foreach (var _ in _sut.EnumerateSectionsAsync(null!)) { }
        };
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EnumerateSectionsAsync_DepthLessThanOne_Throws()
    {
        var act = async () =>
        {
            await foreach (var _ in _sut.EnumerateSectionsAsync("https://forum.example.com", 0)) { }
        };
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task EnumerateSectionsAsync_ReturnsSectionsFromParser()
    {
        var expectedSections = new[]
        {
            new ForumSection { Url = "https://forum.example.com/viewforum.php?f=1", Name = "Section 1", Depth = 1 },
            new ForumSection { Url = "https://forum.example.com/viewforum.php?f=2", Name = "Section 2", Depth = 1 }
        };

        _sectionParser.ParseSections(Arg.Any<string>(), Arg.Any<string>())
            .Returns(expectedSections);

        var results = new List<ForumSection>();
        await foreach (var section in _sut.EnumerateSectionsAsync("https://forum.example.com"))
        {
            results.Add(section);
        }

        results.Should().HaveCount(2);
        results[0].Name.Should().Be("Section 1");
        results[1].Name.Should().Be("Section 2");
    }

    [Fact]
    public async Task EnumerateSectionsAsync_FetchesIndexPhp()
    {
        _sectionParser.ParseSections(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Array.Empty<ForumSection>());

        await foreach (var _ in _sut.EnumerateSectionsAsync("https://forum.example.com")) { }

        _httpHandler.LastRequestUri!.AbsoluteUri.Should().Be("https://forum.example.com/index.php");
    }

    [Fact]
    public async Task EnumerateSectionsAsync_HttpError_ThrowsScrapingException()
    {
        CreateSut("", HttpStatusCode.InternalServerError);

        var act = async () =>
        {
            await foreach (var _ in _sut.EnumerateSectionsAsync("https://forum.example.com")) { }
        };
        await act.Should().ThrowAsync<ScrapingException>();
    }

    [Fact]
    public async Task EnumerateSectionsAsync_Depth2_RecursesIntoSubSections()
    {
        var topSections = new[]
        {
            new ForumSection { Url = "https://forum.example.com/viewforum.php?f=1", Name = "Top", Depth = 1 }
        };

        var subSections = new[]
        {
            new ForumSection { Url = "https://forum.example.com/viewforum.php?f=10", Name = "Sub", Depth = 1 }
        };

        // First call (index page) returns top-level sections
        // Second call (sub-section page) returns sub-sections
        _sectionParser.ParseSections(Arg.Any<string>(), Arg.Any<string>())
            .Returns(topSections, subSections);

        var results = new List<ForumSection>();
        await foreach (var section in _sut.EnumerateSectionsAsync("https://forum.example.com", depth: 2))
        {
            results.Add(section);
        }

        results.Should().HaveCount(2);
        results[0].Name.Should().Be("Top");
        results[0].Depth.Should().Be(1);
        results[1].Name.Should().Be("Sub");
        results[1].Depth.Should().Be(2);
        results[1].ParentUrl.Should().Be("https://forum.example.com/viewforum.php?f=1");
    }

    [Fact]
    public async Task EnumerateSectionsAsync_Cancellation_StopsEnumeration()
    {
        var sections = new[]
        {
            new ForumSection { Url = "https://forum.example.com/viewforum.php?f=1", Name = "S1", Depth = 1 },
            new ForumSection { Url = "https://forum.example.com/viewforum.php?f=2", Name = "S2", Depth = 1 }
        };

        _sectionParser.ParseSections(Arg.Any<string>(), Arg.Any<string>())
            .Returns(sections);

        using var cts = new CancellationTokenSource();
        var results = new List<ForumSection>();

        try
        {
            await foreach (var section in _sut.EnumerateSectionsAsync("https://forum.example.com", cancellationToken: cts.Token))
            {
                results.Add(section);
                cts.Cancel(); // Cancel after first — next MoveNextAsync will throw
            }
        }
        catch (OperationCanceledException)
        {
            // Expected — cancellation triggered on next iteration
        }

        results.Should().HaveCount(1);
    }

    // ===== EnumerateThreadsAsync =====

    [Fact]
    public async Task EnumerateThreadsAsync_NullSectionUrl_Throws()
    {
        var act = async () =>
        {
            await foreach (var _ in _sut.EnumerateThreadsAsync(null!)) { }
        };
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EnumerateThreadsAsync_ParsesPhpBB2Threads()
    {
        var html = @"<html><body>
            <table>
                <tr>
                    <td class='row1'>
                        <a class='topictitle' href='viewtopic.php?t=100'>Movie Download</a>
                    </td>
                </tr>
                <tr>
                    <td class='row1'>
                        <a class='topictitle' href='viewtopic.php?t=200'>Series Episode S01E01</a>
                    </td>
                </tr>
            </table>
        </body></html>";

        CreateSut(html);

        var results = new List<ForumThread>();
        await foreach (var thread in _sut.EnumerateThreadsAsync("https://forum.example.com/viewforum.php?f=1"))
        {
            results.Add(thread);
        }

        results.Should().HaveCount(2);
        results[0].Title.Should().Be("Movie Download");
        results[0].Url.Should().Contain("viewtopic.php?t=100");
        results[1].Title.Should().Be("Series Episode S01E01");
    }

    [Fact]
    public async Task EnumerateThreadsAsync_FallbackToRowClassLinks()
    {
        var html = @"<html><body>
            <table>
                <tr>
                    <td class='row1'>
                        <a href='viewtopic.php?t=300'>Fallback Thread</a>
                    </td>
                </tr>
            </table>
        </body></html>";

        CreateSut(html);

        var results = new List<ForumThread>();
        await foreach (var thread in _sut.EnumerateThreadsAsync("https://forum.example.com/viewforum.php?f=1"))
        {
            results.Add(thread);
        }

        results.Should().HaveCount(1);
        results[0].Title.Should().Be("Fallback Thread");
    }

    [Fact]
    public async Task EnumerateThreadsAsync_NoThreads_ReturnsEmpty()
    {
        CreateSut("<html><body><p>Empty section</p></body></html>");

        var results = new List<ForumThread>();
        await foreach (var thread in _sut.EnumerateThreadsAsync("https://forum.example.com/viewforum.php?f=1"))
        {
            results.Add(thread);
        }

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task EnumerateThreadsAsync_DuplicateThreads_Deduplicated()
    {
        var html = @"<html><body>
            <table>
                <tr><td class='row1'><a class='topictitle' href='viewtopic.php?t=100'>Thread</a></td></tr>
                <tr><td class='row1'><a class='topictitle' href='viewtopic.php?t=100'>Thread</a></td></tr>
            </table>
        </body></html>";

        CreateSut(html);

        var results = new List<ForumThread>();
        await foreach (var thread in _sut.EnumerateThreadsAsync("https://forum.example.com/viewforum.php?f=1"))
        {
            results.Add(thread);
        }

        results.Should().HaveCount(1);
    }

    [Fact]
    public async Task EnumerateThreadsAsync_HttpError_ThrowsScrapingException()
    {
        CreateSut("", HttpStatusCode.InternalServerError);

        var act = async () =>
        {
            await foreach (var _ in _sut.EnumerateThreadsAsync("https://forum.example.com/viewforum.php?f=1")) { }
        };
        await act.Should().ThrowAsync<ScrapingException>();
    }

    [Fact]
    public async Task EnumerateThreadsAsync_WithPagination_FollowsNextPage()
    {
        var page1 = @"<html><body>
            <table>
                <tr><td class='row1'><a class='topictitle' href='viewtopic.php?t=1'>Thread 1</a></td></tr>
            </table>
            <span class='gensmall'><a href='viewforum.php?f=1&amp;start=25'>Next</a></span>
        </body></html>";

        var page2 = @"<html><body>
            <table>
                <tr><td class='row1'><a class='topictitle' href='viewtopic.php?t=2'>Thread 2</a></td></tr>
            </table>
        </body></html>";

        _httpHandler?.Dispose();
        _httpClient?.Dispose();
        _httpHandler = new TestHttpMessageHandler(new Queue<string>(new[] { page1, page2 }));
        _httpClient = new HttpClient(_httpHandler) { BaseAddress = new Uri("https://forum.example.com") };
        _sut = new PhpBB2ForumScraper(_sectionParser, _responseValidator, _logger, _httpClient, _cookieContainer);

        var results = new List<ForumThread>();
        await foreach (var thread in _sut.EnumerateThreadsAsync("https://forum.example.com/viewforum.php?f=1"))
        {
            results.Add(thread);
        }

        results.Should().HaveCount(2);
        results[0].Title.Should().Be("Thread 1");
        results[1].Title.Should().Be("Thread 2");
    }

    // ===== ExtractPostContentAsync =====

    [Fact]
    public async Task ExtractPostContentAsync_NullThreadUrl_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.ExtractPostContentAsync(null!));
    }

    [Fact]
    public async Task ExtractPostContentAsync_ParsesPhpBB2Posts()
    {
        var html = @"<html><body>
            <span class='postbody'>First post content with <b>bold</b> text</span>
            <span class='postbody'>Second post reply</span>
        </body></html>";

        CreateSut(html);

        var posts = await _sut.ExtractPostContentAsync("https://forum.example.com/viewtopic.php?t=100");

        posts.Should().HaveCount(2);
        posts[0].ThreadUrl.Should().Be("https://forum.example.com/viewtopic.php?t=100");
        posts[0].PostIndex.Should().Be(0);
        posts[0].HtmlContent.Should().Contain("bold");
        posts[0].PlainTextContent.Should().Contain("First post content");
        posts[1].PostIndex.Should().Be(1);
        posts[1].PlainTextContent.Should().Contain("Second post reply");
    }

    [Fact]
    public async Task ExtractPostContentAsync_TdPostbody_AlsoParsed()
    {
        var html = @"<html><body>
            <td class='postbody'>Post in table cell</td>
        </body></html>";

        CreateSut(html);

        var posts = await _sut.ExtractPostContentAsync("https://forum.example.com/viewtopic.php?t=100");

        posts.Should().HaveCount(1);
        posts[0].PlainTextContent.Should().Contain("Post in table cell");
    }

    [Fact]
    public async Task ExtractPostContentAsync_DivPostbody_FallbackParsed()
    {
        var html = @"<html><body>
            <div class='postbody'>phpBB3 style post</div>
        </body></html>";

        CreateSut(html);

        var posts = await _sut.ExtractPostContentAsync("https://forum.example.com/viewtopic.php?t=100");

        posts.Should().HaveCount(1);
        posts[0].PlainTextContent.Should().Contain("phpBB3 style post");
    }

    [Fact]
    public async Task ExtractPostContentAsync_NoPosts_ReturnsEmpty()
    {
        CreateSut("<html><body><p>No posts here</p></body></html>");

        var posts = await _sut.ExtractPostContentAsync("https://forum.example.com/viewtopic.php?t=100");

        posts.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractPostContentAsync_EmptyPosts_Skipped()
    {
        var html = @"<html><body>
            <span class='postbody'>   </span>
            <span class='postbody'>Real content</span>
        </body></html>";

        CreateSut(html);

        var posts = await _sut.ExtractPostContentAsync("https://forum.example.com/viewtopic.php?t=100");

        posts.Should().HaveCount(1);
        posts[0].PlainTextContent.Should().Contain("Real content");
    }

    [Fact]
    public async Task ExtractPostContentAsync_HttpError_ThrowsScrapingException()
    {
        CreateSut("", HttpStatusCode.InternalServerError);

        var act = () => _sut.ExtractPostContentAsync("https://forum.example.com/viewtopic.php?t=100");
        await act.Should().ThrowAsync<ScrapingException>();
    }

    // ===== ExtractLinksAsync =====

    [Fact]
    public async Task ExtractLinksAsync_NullPostContent_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.ExtractLinksAsync(null!));
    }

    [Fact]
    public async Task ExtractLinksAsync_ExtractsExternalLinks()
    {
        var post = new PostContent
        {
            ThreadUrl = "https://forum.example.com/viewtopic.php?t=100",
            PostIndex = 0,
            HtmlContent = @"
                <a href='https://download.example.com/file.rar'>Download Here</a>
                <a href='https://mega.nz/file/abc'>Mega Link</a>",
            PlainTextContent = "Download Here Mega Link"
        };

        var links = await _sut.ExtractLinksAsync(post);

        links.Should().HaveCount(2);
        links[0].Url.Should().Be("https://download.example.com/file.rar");
        links[0].Scheme.Should().Be("https");
        links[0].LinkText.Should().Be("Download Here");
        links[1].Url.Should().Be("https://mega.nz/file/abc");
    }

    [Fact]
    public async Task ExtractLinksAsync_SkipsInternalForumLinks()
    {
        var post = new PostContent
        {
            ThreadUrl = "https://forum.example.com/viewtopic.php?t=100",
            PostIndex = 0,
            HtmlContent = @"
                <a href='viewtopic.php?t=200'>Another thread</a>
                <a href='viewforum.php?f=5'>Section</a>
                <a href='login.php'>Login</a>
                <a href='profile.php?mode=viewprofile'>Profile</a>
                <a href='posting.php?mode=reply'>Reply</a>
                <a href='memberlist.php'>Members</a>
                <a href='https://download.example.com/real.zip'>Real Link</a>",
            PlainTextContent = "text"
        };

        var links = await _sut.ExtractLinksAsync(post);

        links.Should().HaveCount(1);
        links[0].Url.Should().Contain("download.example.com");
    }

    [Fact]
    public async Task ExtractLinksAsync_SkipsAnchors()
    {
        var post = new PostContent
        {
            ThreadUrl = "https://forum.example.com/viewtopic.php?t=100",
            PostIndex = 0,
            HtmlContent = "<a href='#top'>Top</a>",
            PlainTextContent = "Top"
        };

        var links = await _sut.ExtractLinksAsync(post);
        links.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractLinksAsync_SkipsRelativeNonAbsoluteLinks()
    {
        var post = new PostContent
        {
            ThreadUrl = "https://forum.example.com/viewtopic.php?t=100",
            PostIndex = 0,
            HtmlContent = "<a href='some/relative/path'>Relative</a>",
            PlainTextContent = "Relative"
        };

        var links = await _sut.ExtractLinksAsync(post);
        links.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractLinksAsync_NoLinks_ReturnsEmpty()
    {
        var post = new PostContent
        {
            ThreadUrl = "https://forum.example.com/viewtopic.php?t=100",
            PostIndex = 0,
            HtmlContent = "<p>Just text, no links</p>",
            PlainTextContent = "Just text, no links"
        };

        var links = await _sut.ExtractLinksAsync(post);
        links.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractLinksAsync_EmptyHref_Skipped()
    {
        var post = new PostContent
        {
            ThreadUrl = "https://forum.example.com/viewtopic.php?t=100",
            PostIndex = 0,
            HtmlContent = "<a href=''>Empty</a><a href='   '>Whitespace</a>",
            PlainTextContent = "Empty Whitespace"
        };

        var links = await _sut.ExtractLinksAsync(post);
        links.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractLinksAsync_HtmlEntitiesDecoded()
    {
        var post = new PostContent
        {
            ThreadUrl = "https://forum.example.com/viewtopic.php?t=100",
            PostIndex = 0,
            HtmlContent = "<a href='https://example.com/file?a=1&amp;b=2'>Link</a>",
            PlainTextContent = "Link"
        };

        var links = await _sut.ExtractLinksAsync(post);
        links.Should().HaveCount(1);
        links[0].Url.Should().Be("https://example.com/file?a=1&b=2");
    }

    [Fact]
    public async Task ExtractLinksAsync_EmptyLinkText_SetsNull()
    {
        var post = new PostContent
        {
            ThreadUrl = "https://forum.example.com/viewtopic.php?t=100",
            PostIndex = 0,
            HtmlContent = "<a href='https://example.com/file.zip'></a>",
            PlainTextContent = ""
        };

        var links = await _sut.ExtractLinksAsync(post);
        links.Should().HaveCount(1);
        links[0].LinkText.Should().BeNull();
    }

    [Fact]
    public async Task ExtractLinksAsync_MagnetLinks_Extracted()
    {
        var post = new PostContent
        {
            ThreadUrl = "https://forum.example.com/viewtopic.php?t=100",
            PostIndex = 0,
            HtmlContent = "<a href='magnet:?xt=urn:btih:abc123'>Torrent</a>",
            PlainTextContent = "Torrent"
        };

        var links = await _sut.ExtractLinksAsync(post);
        links.Should().HaveCount(1);
        links[0].Scheme.Should().Be("magnet");
    }

    // ===== Static HTML Parsing Tests =====

    [Fact]
    public void ParseThreadsFromHtml_TopicTitleLinks()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(@"<html><body>
            <a class='topictitle' href='viewtopic.php?t=1'>Thread One</a>
            <a class='topictitle' href='viewtopic.php?t=2'>Thread Two</a>
        </body></html>");

        var threads = PhpBB2ForumScraper.ParseThreadsFromHtml(doc, "https://forum.example.com/viewforum.php?f=1");

        threads.Should().HaveCount(2);
        threads[0].Title.Should().Be("Thread One");
        threads[1].Title.Should().Be("Thread Two");
    }

    [Fact]
    public void ParseThreadsFromHtml_EmptyTitle_Skipped()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(@"<html><body>
            <a class='topictitle' href='viewtopic.php?t=1'>  </a>
            <a class='topictitle' href='viewtopic.php?t=2'>Valid</a>
        </body></html>");

        var threads = PhpBB2ForumScraper.ParseThreadsFromHtml(doc, "https://forum.example.com/viewforum.php?f=1");

        threads.Should().HaveCount(1);
        threads[0].Title.Should().Be("Valid");
    }

    [Fact]
    public void ParseThreadsFromHtml_EmptyHref_Skipped()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(@"<html><body>
            <a class='topictitle' href=''>No href</a>
        </body></html>");

        var threads = PhpBB2ForumScraper.ParseThreadsFromHtml(doc, "https://forum.example.com/viewforum.php?f=1");
        threads.Should().BeEmpty();
    }

    [Fact]
    public void ParseThreadsFromHtml_ResolvesRelativeUrls()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(@"<html><body>
            <a class='topictitle' href='viewtopic.php?t=5'>Thread</a>
        </body></html>");

        var threads = PhpBB2ForumScraper.ParseThreadsFromHtml(doc, "https://forum.example.com/viewforum.php?f=1");

        threads[0].Url.Should().StartWith("https://forum.example.com/");
        threads[0].Url.Should().Contain("viewtopic.php?t=5");
    }

    [Fact]
    public void ParsePostsFromHtml_SpanPostbody()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(@"<html><body>
            <span class='postbody'>Hello <b>world</b></span>
        </body></html>");

        var posts = PhpBB2ForumScraper.ParsePostsFromHtml(doc, "https://forum.example.com/viewtopic.php?t=1");

        posts.Should().HaveCount(1);
        posts[0].HtmlContent.Should().Contain("<b>world</b>");
        posts[0].PlainTextContent.Should().Contain("Hello world");
    }

    [Fact]
    public void ParsePostsFromHtml_NoPostElements_ReturnsEmpty()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><p>No posts</p></body></html>");

        var posts = PhpBB2ForumScraper.ParsePostsFromHtml(doc, "https://forum.example.com/viewtopic.php?t=1");
        posts.Should().BeEmpty();
    }

    [Fact]
    public void FindNextPageUrl_WithNextLink_ReturnsUrl()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(@"<html><body>
            <span class='gensmall'>
                <a href='viewforum.php?f=1&amp;start=25'>Next</a>
            </span>
        </body></html>");

        var next = PhpBB2ForumScraper.FindNextPageUrl(doc, "https://forum.example.com/viewforum.php?f=1");
        next.Should().NotBeNull();
        next.Should().Contain("start=25");
    }

    [Fact]
    public void FindNextPageUrl_NoNextLink_ReturnsNull()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><p>No pagination</p></body></html>");

        var next = PhpBB2ForumScraper.FindNextPageUrl(doc, "https://forum.example.com/viewforum.php?f=1");
        next.Should().BeNull();
    }

    [Fact]
    public void FindNextPageUrl_WithIconNext_ReturnsUrl()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(@"<html><body>
            <a href='viewforum.php?f=1&amp;start=50'><img src='icon_next.gif' alt='Next'/></a>
        </body></html>");

        var next = PhpBB2ForumScraper.FindNextPageUrl(doc, "https://forum.example.com/viewforum.php?f=1");
        next.Should().NotBeNull();
        next.Should().Contain("start=50");
    }

    // ===== Dispose =====

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var scraper = new PhpBB2ForumScraper(_sectionParser, _responseValidator, _logger);
        scraper.Dispose();
        scraper.Dispose(); // Should not throw
    }

    // ===== TestHttpMessageHandler =====

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private string _responseContent;
        private HttpStatusCode _statusCode;
        private readonly Queue<string>? _responseQueue;

        public Uri? LastRequestUri { get; private set; }
        public HttpMethod? LastRequestMethod { get; private set; }
        public string? LastRequestContent { get; private set; }

        public TestHttpMessageHandler(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseContent = responseContent;
            _statusCode = statusCode;
        }

        public TestHttpMessageHandler(Queue<string> responses, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseQueue = responses;
            _responseContent = "";
            _statusCode = statusCode;
        }

        public void SetNextResponse(string content, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseContent = content;
            _statusCode = statusCode;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastRequestMethod = request.Method;

            if (request.Content is not null)
            {
                LastRequestContent = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            var content = _responseQueue is not null && _responseQueue.Count > 0
                ? _responseQueue.Dequeue()
                : _responseContent;

            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(content)
            };

            if (_statusCode != HttpStatusCode.OK)
            {
                response.EnsureSuccessStatusCode(); // Throw for non-success
            }

            return response;
        }
    }
}
