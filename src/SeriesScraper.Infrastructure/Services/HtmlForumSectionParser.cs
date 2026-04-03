using HtmlAgilityPack;
using SeriesScraper.Domain.Interfaces;
using ForumSectionVO = SeriesScraper.Domain.ValueObjects.ForumSection;

namespace SeriesScraper.Infrastructure.Services;

public class HtmlForumSectionParser : IHtmlForumSectionParser
{
    public IReadOnlyList<ForumSectionVO> ParseSections(string html, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(baseUrl);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Try forum-specific patterns in order of specificity
        // phpBB2 first (uses class='nav' + viewforum.php; phpBB3 uses class='forumtitle')
        var sections = ParsePhpBB2Sections(doc, baseUrl).ToList();
        if (sections.Count > 0)
            return sections.AsReadOnly();

        sections = ParsePhpBBSections(doc, baseUrl).ToList();
        if (sections.Count > 0)
            return sections.AsReadOnly();

        sections = ParseVBulletinSections(doc, baseUrl).ToList();
        if (sections.Count > 0)
            return sections.AsReadOnly();

        sections = ParseXenForoSections(doc, baseUrl).ToList();
        if (sections.Count > 0)
            return sections.AsReadOnly();

        // Generic fallback
        sections = ParseGenericForumSections(doc, baseUrl).ToList();
        return sections.AsReadOnly();
    }

    private static IEnumerable<ForumSectionVO> ParsePhpBB2Sections(
        HtmlDocument doc, string baseUrl)
    {
        // phpBB2: <a class='nav' href='viewforum.php?f=324&sid=...'>Forum Name</a>
        // Breadcrumbs also use class='nav' but link to index.php, so we filter on viewforum.php
        var navLinks = doc.DocumentNode.SelectNodes(
            "//a[@class='nav' and contains(@href, 'viewforum.php')]");
        if (navLinks == null) yield break;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in navLinks)
        {
            var href = HtmlEntity.DeEntitize(node.GetAttributeValue("href", ""));
            var name = HtmlEntity.DeEntitize(node.InnerText).Trim();

            if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(name))
                continue;

            var resolvedUrl = ResolveUrl(baseUrl, href);
            if (!seen.Add(resolvedUrl))
                continue;

            yield return new ForumSectionVO
            {
                Url = resolvedUrl,
                Name = name,
                Depth = 1
            };
        }
    }

    private static IEnumerable<ForumSectionVO> ParsePhpBBSections(
        HtmlDocument doc, string baseUrl)
    {
        // phpBB: <a class="forumtitle" href="./viewforum.php?f=1">Name</a>
        var nodes = doc.DocumentNode.SelectNodes(
            "//a[contains(@class, 'forumtitle')]");
        if (nodes == null) yield break;

        foreach (var node in nodes)
        {
            var href = node.GetAttributeValue("href", "");
            var name = HtmlEntity.DeEntitize(node.InnerText).Trim();

            if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(name))
                continue;

            yield return new ForumSectionVO
            {
                Url = ResolveUrl(baseUrl, href),
                Name = name,
                Depth = 1
            };
        }
    }

    private static IEnumerable<ForumSectionVO> ParseVBulletinSections(
        HtmlDocument doc, string baseUrl)
    {
        // vBulletin: links within forum listing structures
        var nodes = doc.DocumentNode.SelectNodes(
            "//a[contains(@href, 'forumdisplay.php')]" +
            " | //h2[contains(@class, 'forumtitle')]/a" +
            " | //td[contains(@id, 'f')]//a[strong]");
        if (nodes == null) yield break;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            var href = node.GetAttributeValue("href", "");
            var name = HtmlEntity.DeEntitize(node.InnerText).Trim();

            if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(name))
                continue;

            var resolvedUrl = ResolveUrl(baseUrl, href);
            if (!seen.Add(resolvedUrl))
                continue;

            yield return new ForumSectionVO
            {
                Url = resolvedUrl,
                Name = name,
                Depth = 1
            };
        }
    }

    private static IEnumerable<ForumSectionVO> ParseXenForoSections(
        HtmlDocument doc, string baseUrl)
    {
        // XenForo: <h3 class="node-title"><a href="/forums/name.1/">Name</a></h3>
        var nodes = doc.DocumentNode.SelectNodes(
            "//h3[contains(@class, 'node-title')]/a" +
            " | //div[contains(@class, 'node--forum')]//a[contains(@class, 'node-title')]");
        if (nodes == null) yield break;

        foreach (var node in nodes)
        {
            var href = node.GetAttributeValue("href", "");
            var name = HtmlEntity.DeEntitize(node.InnerText).Trim();

            if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(name))
                continue;

            yield return new ForumSectionVO
            {
                Url = ResolveUrl(baseUrl, href),
                Name = name,
                Depth = 1
            };
        }
    }

    private static IEnumerable<ForumSectionVO> ParseGenericForumSections(
        HtmlDocument doc, string baseUrl)
    {
        // Generic: look for links with forum-like URL patterns
        var nodes = doc.DocumentNode.SelectNodes("//a[@href]");
        if (nodes == null) yield break;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            var href = node.GetAttributeValue("href", "").Trim();
            var name = HtmlEntity.DeEntitize(node.InnerText).Trim();

            if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(name))
                continue;

            if (!IsForumSectionUrl(href))
                continue;

            var resolvedUrl = ResolveUrl(baseUrl, href);
            if (!seen.Add(resolvedUrl))
                continue;

            yield return new ForumSectionVO
            {
                Url = resolvedUrl,
                Name = name,
                Depth = 1
            };
        }
    }

    internal static bool IsForumSectionUrl(string href)
    {
        var lower = href.ToLowerInvariant();
        return lower.Contains("forum") ||
               lower.Contains("viewforum") ||
               lower.Contains("forumdisplay") ||
               lower.Contains("board") ||
               lower.Contains("/forums/");
    }

    internal static string ResolveUrl(string baseUrl, string href)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var absolute))
            return absolute.ToString();

        // Remove ./ prefix common in phpBB
        if (href.StartsWith("./"))
            href = href[2..];

        if (Uri.TryCreate(new Uri(baseUrl), href, out var resolved))
            return resolved.ToString();

        return href;
    }
}
