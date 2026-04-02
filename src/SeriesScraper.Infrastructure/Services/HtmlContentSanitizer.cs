using Ganss.Xss;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Infrastructure.Services;

/// <summary>
/// Sanitizes HTML content using HtmlSanitizer to prevent XSS attacks.
/// Removes script tags, event handlers, data URIs, and other dangerous content.
/// </summary>
public sealed class HtmlContentSanitizer : ISanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlContentSanitizer()
    {
        _sanitizer = new HtmlSanitizer();

        // Remove data: URIs from allowed URI schemes (block data URI XSS vectors)
        _sanitizer.AllowedSchemes.Remove("data");

        // Remove javascript: scheme explicitly
        _sanitizer.AllowedSchemes.Remove("javascript");

        // Remove vbscript: scheme
        _sanitizer.AllowedSchemes.Remove("vbscript");

        // Only allow safe schemes
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("mailto");

        // Remove potentially dangerous tags that HtmlSanitizer may allow by default
        _sanitizer.AllowedTags.Remove("script");
        _sanitizer.AllowedTags.Remove("object");
        _sanitizer.AllowedTags.Remove("embed");
        _sanitizer.AllowedTags.Remove("form");
        _sanitizer.AllowedTags.Remove("input");
        _sanitizer.AllowedTags.Remove("textarea");
        _sanitizer.AllowedTags.Remove("button");
        _sanitizer.AllowedTags.Remove("select");
        _sanitizer.AllowedTags.Remove("option");

        // Remove event handler attributes (on*)
        // HtmlSanitizer strips these by default, but be explicit
        _sanitizer.AllowedAttributes.Remove("onerror");
        _sanitizer.AllowedAttributes.Remove("onload");
        _sanitizer.AllowedAttributes.Remove("onclick");
        _sanitizer.AllowedAttributes.Remove("onmouseover");
    }

    public string SanitizeHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        return _sanitizer.Sanitize(html);
    }
}
