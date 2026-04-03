using System.Net;
using System.Runtime.CompilerServices;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Exceptions;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Infrastructure.Services.Scrapers;

/// <summary>
/// Concrete IForumScraper implementation for phpBB2-based forums.
/// Handles authentication, section/thread enumeration, post extraction,
/// and link extraction against phpBB2 HTML structures.
/// </summary>
public sealed class PhpBB2ForumScraper : IForumScraper, IDisposable
{
    private readonly IHtmlForumSectionParser _sectionParser;
    private readonly IResponseValidator _responseValidator;
    private readonly ILogger<PhpBB2ForumScraper> _logger;

    private readonly CookieContainer _cookieContainer;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private string? _authenticatedBaseUrl;

    public PhpBB2ForumScraper(
        IHtmlForumSectionParser sectionParser,
        IResponseValidator responseValidator,
        ILogger<PhpBB2ForumScraper> logger)
    {
        _sectionParser = sectionParser ?? throw new ArgumentNullException(nameof(sectionParser));
        _responseValidator = responseValidator ?? throw new ArgumentNullException(nameof(responseValidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = _cookieContainer,
            UseCookies = true,
            AllowAutoRedirect = true
        };
        _httpClient = new HttpClient(handler);
        _ownsHttpClient = true;
    }

    /// <summary>
    /// Internal constructor for unit testing — allows injecting a custom HttpClient.
    /// </summary>
    internal PhpBB2ForumScraper(
        IHtmlForumSectionParser sectionParser,
        IResponseValidator responseValidator,
        ILogger<PhpBB2ForumScraper> logger,
        HttpClient httpClient,
        CookieContainer cookieContainer)
    {
        _sectionParser = sectionParser ?? throw new ArgumentNullException(nameof(sectionParser));
        _responseValidator = responseValidator ?? throw new ArgumentNullException(nameof(responseValidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cookieContainer = cookieContainer ?? throw new ArgumentNullException(nameof(cookieContainer));
        _ownsHttpClient = false;
    }

    /// <inheritdoc />
    public async Task<bool> AuthenticateAsync(ForumCredentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        if (string.IsNullOrWhiteSpace(credentials.BaseUrl))
            throw new ScrapingException("BaseUrl must be set on ForumCredentials for phpBB2 authentication.");

        var baseUrl = credentials.BaseUrl.TrimEnd('/');
        var loginUrl = $"{baseUrl}/login.php";

        _logger.LogDebug("Authenticating to phpBB2 forum at {LoginUrl} as {Username}", loginUrl, credentials.Username);

        try
        {
            var formData = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = credentials.Username,
                ["password"] = credentials.Password,
                ["login"] = "Log in",
                ["autologin"] = "on"
            });

            var response = await _httpClient.PostAsync(loginUrl, formData, cancellationToken);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            // If the response is still a login page, authentication failed
            if (_responseValidator.IsSessionExpired(html))
            {
                _logger.LogWarning("Authentication failed for phpBB2 forum at {BaseUrl} — login page returned", baseUrl);
                return false;
            }

            _authenticatedBaseUrl = baseUrl;
            _logger.LogInformation("Successfully authenticated to phpBB2 forum at {BaseUrl}", baseUrl);
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during phpBB2 authentication at {LoginUrl}", loginUrl);
            throw new ScrapingException($"Network error during authentication to {loginUrl}", ex);
        }
    }

    /// <inheritdoc />
    public CookieContainer GetCookieContainer() => _cookieContainer;

    /// <inheritdoc />
    public async Task<bool> ValidateSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_authenticatedBaseUrl is null)
            return false;

        try
        {
            var indexUrl = $"{_authenticatedBaseUrl}/index.php";
            var html = await _httpClient.GetStringAsync(indexUrl, cancellationToken);
            return !_responseValidator.IsSessionExpired(html);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to validate session for phpBB2 forum at {BaseUrl}", _authenticatedBaseUrl);
            return false;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ForumSection> EnumerateSectionsAsync(
        string baseUrl,
        int depth = 1,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        if (depth < 1) throw new ArgumentOutOfRangeException(nameof(depth), "Depth must be at least 1.");

        var normalizedBase = baseUrl.TrimEnd('/');

        _logger.LogDebug("Enumerating phpBB2 sections at {BaseUrl} with depth {Depth}", normalizedBase, depth);

        string html;
        try
        {
            var indexUrl = $"{normalizedBase}/index.php";
            html = await _httpClient.GetStringAsync(indexUrl, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ScrapingException($"Failed to fetch forum index at {normalizedBase}", ex);
        }

        var sections = _sectionParser.ParseSections(html, normalizedBase);

        foreach (var section in sections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return section;

            // Recurse for deeper levels
            if (depth > 1)
            {
                await foreach (var subSection in EnumerateSubSectionsAsync(
                    section.Url, normalizedBase, depth - 1, 2, cancellationToken))
                {
                    yield return subSection;
                }
            }
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ForumThread> EnumerateThreadsAsync(
        string sectionUrl,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sectionUrl);

        _logger.LogDebug("Enumerating threads in phpBB2 section {SectionUrl}", sectionUrl);

        var currentUrl = sectionUrl;

        while (currentUrl is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string html;
            try
            {
                html = await _httpClient.GetStringAsync(currentUrl, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new ScrapingException($"Failed to fetch section page at {currentUrl}", ex);
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var threads = ParseThreadsFromHtml(doc, sectionUrl);
            foreach (var thread in threads)
            {
                yield return thread;
            }

            // Check for next page
            currentUrl = FindNextPageUrl(doc, sectionUrl);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PostContent>> ExtractPostContentAsync(
        string threadUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(threadUrl);

        _logger.LogDebug("Extracting post content from phpBB2 thread {ThreadUrl}", threadUrl);

        string html;
        try
        {
            html = await _httpClient.GetStringAsync(threadUrl, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ScrapingException($"Failed to fetch thread at {threadUrl}", ex);
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        return ParsePostsFromHtml(doc, threadUrl);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ExtractedLink>> ExtractLinksAsync(
        PostContent postContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(postContent);

        var doc = new HtmlDocument();
        doc.LoadHtml(postContent.HtmlContent);

        var links = new List<ExtractedLink>();

        var anchorNodes = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchorNodes is not null)
        {
            foreach (var anchor in anchorNodes)
            {
                var href = HtmlEntity.DeEntitize(anchor.GetAttributeValue("href", "")).Trim();
                if (string.IsNullOrWhiteSpace(href))
                    continue;

                // Skip internal forum links and anchors
                if (href.StartsWith('#') ||
                    href.Contains("viewtopic.php", StringComparison.OrdinalIgnoreCase) ||
                    href.Contains("viewforum.php", StringComparison.OrdinalIgnoreCase) ||
                    href.Contains("login.php", StringComparison.OrdinalIgnoreCase) ||
                    href.Contains("profile.php", StringComparison.OrdinalIgnoreCase) ||
                    href.Contains("posting.php", StringComparison.OrdinalIgnoreCase) ||
                    href.Contains("memberlist.php", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!Uri.TryCreate(href, UriKind.Absolute, out var uri))
                    continue;

                var linkText = HtmlEntity.DeEntitize(anchor.InnerText).Trim();

                links.Add(new ExtractedLink
                {
                    Url = uri.AbsoluteUri,
                    Scheme = uri.Scheme,
                    LinkText = string.IsNullOrWhiteSpace(linkText) ? null : linkText
                });
            }
        }

        return Task.FromResult<IReadOnlyList<ExtractedLink>>(links.AsReadOnly());
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    // --- Private helpers ---

    private async IAsyncEnumerable<ForumSection> EnumerateSubSectionsAsync(
        string sectionUrl,
        string baseUrl,
        int remainingDepth,
        int currentDepth,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string html;
        try
        {
            html = await _httpClient.GetStringAsync(sectionUrl, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch sub-section at {SectionUrl}, skipping", sectionUrl);
            yield break;
        }

        var subSections = _sectionParser.ParseSections(html, baseUrl);

        foreach (var sub in subSections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Adjust depth for the sub-section
            var adjusted = sub with { Depth = currentDepth, ParentUrl = sectionUrl };
            yield return adjusted;

            if (remainingDepth > 1)
            {
                await foreach (var deeper in EnumerateSubSectionsAsync(
                    adjusted.Url, baseUrl, remainingDepth - 1, currentDepth + 1, cancellationToken))
                {
                    yield return deeper;
                }
            }
        }
    }

    internal static IReadOnlyList<ForumThread> ParseThreadsFromHtml(HtmlDocument doc, string sectionUrl)
    {
        var threads = new List<ForumThread>();

        // phpBB2: threads are links with class 'topictitle' pointing to viewtopic.php
        var topicNodes = doc.DocumentNode.SelectNodes(
            "//a[contains(@class, 'topictitle') and contains(@href, 'viewtopic.php')]");

        if (topicNodes is null)
        {
            // Fallback: any viewtopic.php link inside topic row areas
            topicNodes = doc.DocumentNode.SelectNodes(
                "//td[contains(@class, 'row')]//a[contains(@href, 'viewtopic.php')]");
        }

        if (topicNodes is null)
            return threads.AsReadOnly();

        var baseUri = new Uri(sectionUrl);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in topicNodes)
        {
            var href = HtmlEntity.DeEntitize(node.GetAttributeValue("href", "")).Trim();
            var title = HtmlEntity.DeEntitize(node.InnerText).Trim();

            if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(title))
                continue;

            var resolvedUrl = ResolveUrl(baseUri, href);
            if (!seen.Add(resolvedUrl))
                continue;

            threads.Add(new ForumThread
            {
                Url = resolvedUrl,
                Title = title
            });
        }

        return threads.AsReadOnly();
    }

    internal static IReadOnlyList<PostContent> ParsePostsFromHtml(HtmlDocument doc, string threadUrl)
    {
        var posts = new List<PostContent>();

        // phpBB2: post bodies are in <span class="postbody"> or <td class="postbody">
        var postNodes = doc.DocumentNode.SelectNodes(
            "//span[contains(@class, 'postbody')] | //td[contains(@class, 'postbody')]");

        // Fallback: look for div.postbody (phpBB3 style)
        postNodes ??= doc.DocumentNode.SelectNodes("//div[contains(@class, 'postbody')]");

        if (postNodes is null)
            return posts.AsReadOnly();

        int index = 0;
        foreach (var node in postNodes)
        {
            var htmlContent = node.InnerHtml.Trim();
            var plainText = HtmlEntity.DeEntitize(node.InnerText).Trim();

            if (string.IsNullOrWhiteSpace(plainText))
                continue;

            posts.Add(new PostContent
            {
                ThreadUrl = threadUrl,
                PostIndex = index++,
                HtmlContent = htmlContent,
                PlainTextContent = plainText
            });
        }

        return posts.AsReadOnly();
    }

    internal static string? FindNextPageUrl(HtmlDocument doc, string currentUrl)
    {
        // phpBB2 pagination: look for "next" link in pagination area
        // Pattern: <a href="viewforum.php?f=...&start=...">Next</a> or arrow image
        var nextLink = doc.DocumentNode.SelectSingleNode(
            "//span[@class='gensmall']//a[contains(text(), 'Next') or contains(text(), 'next')]");

        // Also check for arrow-based navigation
        nextLink ??= doc.DocumentNode.SelectSingleNode(
            "//a[img[contains(@src, 'icon_next') or contains(@alt, 'Next')]]");

        if (nextLink is null)
            return null;

        var href = HtmlEntity.DeEntitize(nextLink.GetAttributeValue("href", "")).Trim();
        if (string.IsNullOrWhiteSpace(href))
            return null;

        return ResolveUrl(new Uri(currentUrl), href);
    }

    private static string ResolveUrl(Uri baseUri, string relativeUrl)
    {
        if (Uri.TryCreate(relativeUrl, UriKind.Absolute, out var absolute))
            return absolute.AbsoluteUri;

        if (Uri.TryCreate(baseUri, relativeUrl, out var resolved))
            return resolved.AbsoluteUri;

        return relativeUrl;
    }
}
