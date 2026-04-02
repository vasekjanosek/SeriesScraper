using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;
using SeriesScraper.Infrastructure.Services;

namespace SeriesScraper.Infrastructure.Tests.Services;

public class ForumPostScraperTests
{
    private readonly IForumSessionManager _sessionManager;
    private readonly IForumScraper _forumScraper;
    private readonly ILinkExtractorService _linkExtractor;
    private readonly ILogger<ForumPostScraper> _logger;
    private readonly ForumPostScraper _sut;

    private readonly Forum _testForum = new()
    {
        ForumId = 1,
        Name = "TestForum",
        BaseUrl = "https://forum.example.com",
        Username = "user",
        CredentialKey = "FORUM_PASS"
    };

    public ForumPostScraperTests()
    {
        _sessionManager = Substitute.For<IForumSessionManager>();
        _forumScraper = Substitute.For<IForumScraper>();
        _linkExtractor = Substitute.For<ILinkExtractorService>();
        _logger = Substitute.For<ILogger<ForumPostScraper>>();

        _sessionManager.GetAuthenticatedClientAsync(Arg.Any<Forum>(), Arg.Any<CancellationToken>())
            .Returns(new HttpClient());

        _sut = new ForumPostScraper(_sessionManager, _forumScraper, _linkExtractor, _logger);
    }

    // --- Constructor Validation ---

    [Fact]
    public void Constructor_NullSessionManager_Throws()
    {
        var act = () => new ForumPostScraper(null!, _forumScraper, _linkExtractor, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("sessionManager");
    }

    [Fact]
    public void Constructor_NullForumScraper_Throws()
    {
        var act = () => new ForumPostScraper(_sessionManager, null!, _linkExtractor, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("forumScraper");
    }

    [Fact]
    public void Constructor_NullLinkExtractor_Throws()
    {
        var act = () => new ForumPostScraper(_sessionManager, _forumScraper, null!, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("linkExtractor");
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var act = () => new ForumPostScraper(_sessionManager, _forumScraper, _linkExtractor, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // --- ScrapePostAsync: Argument validation ---

    [Fact]
    public async Task ScrapePostAsync_NullForum_ThrowsArgumentNull()
    {
        var act = () => _sut.ScrapePostAsync(null!, "https://example.com/thread/1", 1);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ScrapePostAsync_NullPostUrl_ReturnsFailure()
    {
        var result = await _sut.ScrapePostAsync(_testForum, null!, 1);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("null or empty");
    }

    [Fact]
    public async Task ScrapePostAsync_EmptyPostUrl_ReturnsFailure()
    {
        var result = await _sut.ScrapePostAsync(_testForum, "", 1);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("null or empty");
    }

    [Fact]
    public async Task ScrapePostAsync_WhitespacePostUrl_ReturnsFailure()
    {
        var result = await _sut.ScrapePostAsync(_testForum, "   ", 1);
        result.Success.Should().BeFalse();
    }

    // --- ScrapePostAsync: Authentication ---

    [Fact]
    public async Task ScrapePostAsync_AuthenticatesSession()
    {
        var postUrl = "https://forum.example.com/thread/1";
        _forumScraper.ExtractPostContentAsync(postUrl, Arg.Any<CancellationToken>())
            .Returns(new List<PostContent>());

        await _sut.ScrapePostAsync(_testForum, postUrl, 1);

        await _sessionManager.Received(1).GetAuthenticatedClientAsync(_testForum, Arg.Any<CancellationToken>());
    }

    // --- ScrapePostAsync: Success path ---

    [Fact]
    public async Task ScrapePostAsync_WithPosts_ExtractsLinksFromEachPost()
    {
        var postUrl = "https://forum.example.com/thread/1";
        var posts = new List<PostContent>
        {
            new() { ThreadUrl = postUrl, PostIndex = 0, HtmlContent = "<p>Post 1 <a href='https://dl.example.com/f1.mkv'>link</a></p>", PlainTextContent = "Post 1" },
            new() { ThreadUrl = postUrl, PostIndex = 1, HtmlContent = "<p>Post 2 <a href='https://dl.example.com/f2.mkv'>link</a></p>", PlainTextContent = "Post 2" }
        };

        _forumScraper.ExtractPostContentAsync(postUrl, Arg.Any<CancellationToken>())
            .Returns(posts);

        var linksPost1 = new List<Link>
        {
            new() { Url = "https://dl.example.com/f1.mkv", PostUrl = postUrl, LinkTypeId = 1, RunId = 1 }
        };
        var linksPost2 = new List<Link>
        {
            new() { Url = "https://dl.example.com/f2.mkv", PostUrl = postUrl, LinkTypeId = 1, RunId = 1 }
        };

        _linkExtractor.ExtractLinksAsync(posts[0].HtmlContent, 1, postUrl, Arg.Any<CancellationToken>())
            .Returns(linksPost1);
        _linkExtractor.ExtractLinksAsync(posts[1].HtmlContent, 1, postUrl, Arg.Any<CancellationToken>())
            .Returns(linksPost2);

        var result = await _sut.ScrapePostAsync(_testForum, postUrl, 1);

        result.Success.Should().BeTrue();
        result.PostUrl.Should().Be(postUrl);
        result.ExtractedLinks.Should().HaveCount(2);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ScrapePostAsync_WithNoPosts_ReturnsFailure()
    {
        var postUrl = "https://forum.example.com/thread/1";
        _forumScraper.ExtractPostContentAsync(postUrl, Arg.Any<CancellationToken>())
            .Returns(new List<PostContent>());

        var result = await _sut.ScrapePostAsync(_testForum, postUrl, 1);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No post content found");
    }

    [Fact]
    public async Task ScrapePostAsync_WithPostsButNoLinks_ReturnsSuccessWithEmptyLinks()
    {
        var postUrl = "https://forum.example.com/thread/1";
        var posts = new List<PostContent>
        {
            new() { ThreadUrl = postUrl, PostIndex = 0, HtmlContent = "<p>Just text</p>", PlainTextContent = "Just text" }
        };

        _forumScraper.ExtractPostContentAsync(postUrl, Arg.Any<CancellationToken>())
            .Returns(posts);
        _linkExtractor.ExtractLinksAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Link>());

        var result = await _sut.ScrapePostAsync(_testForum, postUrl, 1);

        result.Success.Should().BeTrue();
        result.ExtractedLinks.Should().BeEmpty();
    }

    // --- ScrapePostAsync: Error handling ---

    [Fact]
    public async Task ScrapePostAsync_ForumScraperThrows_ReturnsFailure()
    {
        var postUrl = "https://forum.example.com/thread/1";
        _forumScraper.ExtractPostContentAsync(postUrl, Arg.Any<CancellationToken>())
            .Throws(new Exception("Network timeout"));

        var result = await _sut.ScrapePostAsync(_testForum, postUrl, 1);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Network timeout");
        result.PostUrl.Should().Be(postUrl);
    }

    [Fact]
    public async Task ScrapePostAsync_LinkExtractorThrows_ReturnsFailure()
    {
        var postUrl = "https://forum.example.com/thread/1";
        var posts = new List<PostContent>
        {
            new() { ThreadUrl = postUrl, PostIndex = 0, HtmlContent = "<p>Content</p>", PlainTextContent = "Content" }
        };

        _forumScraper.ExtractPostContentAsync(postUrl, Arg.Any<CancellationToken>())
            .Returns(posts);
        _linkExtractor.ExtractLinksAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("Parse error"));

        var result = await _sut.ScrapePostAsync(_testForum, postUrl, 1);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Parse error");
    }

    [Fact]
    public async Task ScrapePostAsync_AuthenticationFails_ReturnsFailure()
    {
        var postUrl = "https://forum.example.com/thread/1";
        _sessionManager.GetAuthenticatedClientAsync(Arg.Any<Forum>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Auth failed"));

        var result = await _sut.ScrapePostAsync(_testForum, postUrl, 1);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Auth failed");
    }

    // --- ScrapePostAsync: Cancellation ---

    [Fact]
    public async Task ScrapePostAsync_WhenCancelled_ThrowsOperationCanceled()
    {
        var postUrl = "https://forum.example.com/thread/1";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _forumScraper.ExtractPostContentAsync(postUrl, Arg.Any<CancellationToken>())
            .Returns(new List<PostContent>
            {
                new() { ThreadUrl = postUrl, PostIndex = 0, HtmlContent = "<p>text</p>", PlainTextContent = "text" }
            });

        _linkExtractor.ExtractLinksAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());

        var act = () => _sut.ScrapePostAsync(_testForum, postUrl, 1, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // --- ScrapePostAsync: Multiple links from multiple posts ---

    [Fact]
    public async Task ScrapePostAsync_MultiplePosts_CombinesAllLinks()
    {
        var postUrl = "https://forum.example.com/thread/1";
        var posts = new List<PostContent>
        {
            new() { ThreadUrl = postUrl, PostIndex = 0, HtmlContent = "<p>Post 1</p>", PlainTextContent = "Post 1" },
            new() { ThreadUrl = postUrl, PostIndex = 1, HtmlContent = "<p>Post 2</p>", PlainTextContent = "Post 2" },
            new() { ThreadUrl = postUrl, PostIndex = 2, HtmlContent = "<p>Post 3</p>", PlainTextContent = "Post 3" }
        };

        _forumScraper.ExtractPostContentAsync(postUrl, Arg.Any<CancellationToken>())
            .Returns(posts);

        _linkExtractor.ExtractLinksAsync(Arg.Any<string>(), 1, postUrl, Arg.Any<CancellationToken>())
            .Returns(ci => new List<Link>
            {
                new() { Url = $"https://dl.example.com/from-post-{posts.IndexOf(posts.First(p => p.HtmlContent == ci.ArgAt<string>(0)))}.mkv", PostUrl = postUrl, LinkTypeId = 1, RunId = 1 }
            });

        var result = await _sut.ScrapePostAsync(_testForum, postUrl, 1);

        result.Success.Should().BeTrue();
        result.ExtractedLinks.Should().HaveCount(3);
    }

    // --- PostScrapeResult value object ---

    [Fact]
    public void PostScrapeResult_Succeeded_HasCorrectProperties()
    {
        var links = new List<Link>
        {
            new() { Url = "https://example.com/file.mkv", PostUrl = "url", LinkTypeId = 1, RunId = 1 }
        };
        var result = PostScrapeResult.Succeeded("https://example.com/thread/1", links);

        result.Success.Should().BeTrue();
        result.PostUrl.Should().Be("https://example.com/thread/1");
        result.ExtractedLinks.Should().HaveCount(1);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void PostScrapeResult_Failed_HasCorrectProperties()
    {
        var result = PostScrapeResult.Failed("https://example.com/thread/1", "Something went wrong");

        result.Success.Should().BeFalse();
        result.PostUrl.Should().Be("https://example.com/thread/1");
        result.ExtractedLinks.Should().BeEmpty();
        result.ErrorMessage.Should().Be("Something went wrong");
    }
}
