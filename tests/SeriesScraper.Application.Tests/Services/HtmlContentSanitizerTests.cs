using FluentAssertions;
using SeriesScraper.Application.Services;

namespace SeriesScraper.Application.Tests.Services;

public class HtmlContentSanitizerTests
{
    private readonly HtmlContentSanitizer _sanitizer = new();

    [Fact]
    public void SanitizeHtml_NullInput_ReturnsEmpty()
    {
        _sanitizer.SanitizeHtml(null!).Should().BeEmpty();
    }

    [Fact]
    public void SanitizeHtml_EmptyString_ReturnsEmpty()
    {
        _sanitizer.SanitizeHtml(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void SanitizeHtml_PlainText_ReturnsSameText()
    {
        var input = "Hello World - this is plain text";
        _sanitizer.SanitizeHtml(input).Should().Be(input);
    }

    [Fact]
    public void SanitizeHtml_SafeHtml_PreservesContent()
    {
        var input = "<p>This is <strong>bold</strong> and <em>italic</em>.</p>";
        var result = _sanitizer.SanitizeHtml(input);
        result.Should().Contain("<p>");
        result.Should().Contain("<strong>bold</strong>");
        result.Should().Contain("<em>italic</em>");
    }

    [Fact]
    public void SanitizeHtml_SafeLinks_PreservesHttpLinks()
    {
        var input = """<a href="https://example.com">Link</a>""";
        var result = _sanitizer.SanitizeHtml(input);
        result.Should().Contain("https://example.com");
    }

    // --- XSS Attack Vectors ---

    [Fact]
    public void SanitizeHtml_ScriptTag_Removed()
    {
        var input = """<p>Hello</p><script>alert('xss')</script><p>World</p>""";
        var result = _sanitizer.SanitizeHtml(input);
        result.Should().NotContain("<script");
        result.Should().NotContain("alert");
        result.Should().Contain("Hello");
        result.Should().Contain("World");
    }

    [Fact]
    public void SanitizeHtml_ScriptTagUppercase_Removed()
    {
        var input = """<SCRIPT>alert('xss')</SCRIPT>""";
        var result = _sanitizer.SanitizeHtml(input);
        result.Should().NotContainEquivalentOf("<script");
        result.Should().NotContain("alert");
    }

    [Fact]
    public void SanitizeHtml_OnErrorHandler_Removed()
    {
        var input = """<img src="x" onerror="alert('xss')">""";
        var result = _sanitizer.SanitizeHtml(input);
        result.Should().NotContain("onerror");
        result.Should().NotContain("alert");
    }

    [Fact]
    public void SanitizeHtml_OnClickHandler_Removed()
    {
        var input = """<div onclick="alert('xss')">Click me</div>""";
        var result = _sanitizer.SanitizeHtml(input);
        result.Should().NotContain("onclick");
        result.Should().NotContain("alert");
    }

    [Fact]
    public void SanitizeHtml_OnMouseOverHandler_Removed()
    {
        var input = """<span onmouseover="alert('xss')">Hover</span>""";
        var result = _sanitizer.SanitizeHtml(input);
        result.Should().NotContain("onmouseover");
        result.Should().NotContain("alert");
    }

    [Fact]
    public void SanitizeHtml_OnLoadHandler_Removed()
    {
        var input = """<body onload="alert('xss')">""";
        var result = _sanitizer.SanitizeHtml(input);
        result.Should().NotContain("onload");
        result.Should().NotContain("alert");
    }

    [Fact]
    public void SanitizeHtml_DataUri_Removed()
    {
        var input = """<a href="data:text/html,<script>alert('xss')</script>">Click</a>""";
        var result = _sanitizer.SanitizeHtml(input);
        result.Should().NotContain("data:");
        result.Should().NotContain("alert");
    }

    [Fact]
    public void SanitizeHtml_JavascriptUri_Removed()
    {
        var input = """<a href="javascript:alert('xss')">Click</a>""";
        var result = _sanitizer.SanitizeHtml(input);
        result.Should().NotContain("javascript:");
        result.Should().NotContain("alert");
    }

    [Fact]
    public void SanitizeHtml_VbscriptUri_Removed()
    {
        var input = """<a href="vbscript:msgbox('xss')">Click</a>""";
        var result = _sanitizer.SanitizeHtml(input);
        result.Should().NotContain("vbscript:");
    }

    [Fact]
    public void SanitizeHtml_EmbedTag_Removed()
    {
        var input = """<embed src="evil.swf" type="application/x-shockwave-flash">""";
        var result = _sanitizer.SanitizeHtml(input);
        result.Should().NotContain("<embed");
    }

    [Fact]
    public void SanitizeHtml_ObjectTag_Removed()
    {
        var input = """<object data="evil.swf"></object>""";
        var result = _sanitizer.SanitizeHtml(input);
        result.Should().NotContain("<object");
    }

    [Fact]
    public void SanitizeHtml_FormTag_Removed()
    {
        var input = """<form action="https://evil.com"><input type="text" name="password"></form>""";
        var result = _sanitizer.SanitizeHtml(input);
        result.Should().NotContain("<form");
        result.Should().NotContain("<input");
    }

    [Fact]
    public void SanitizeHtml_NestedScriptInAttributes_Removed()
    {
        var input = """<img src="x" onerror="var s=document.createElement('script');s.src='evil.js';document.body.appendChild(s)">""";
        var result = _sanitizer.SanitizeHtml(input);
        result.Should().NotContain("onerror");
        result.Should().NotContain("createElement");
    }

    [Fact]
    public void SanitizeHtml_SvgOnload_Removed()
    {
        var input = """<svg onload="alert('xss')"><circle r="10"/></svg>""";
        var result = _sanitizer.SanitizeHtml(input);
        result.Should().NotContain("onload");
        result.Should().NotContain("alert");
    }

    [Fact]
    public void SanitizeHtml_StyleExpression_Removed()
    {
        var input = """<div style="background: url(javascript:alert('xss'))">Text</div>""";
        var result = _sanitizer.SanitizeHtml(input);
        result.Should().NotContain("javascript:");
    }

    [Fact]
    public void SanitizeHtml_MixedContent_PreservesSafeRemovesDangerous()
    {
        var input = """
            <div>
                <p>Safe paragraph</p>
                <script>alert('xss')</script>
                <a href="https://example.com">Safe link</a>
                <img src="photo.jpg" onerror="alert('xss')">
                <strong>Bold text</strong>
            </div>
            """;
        var result = _sanitizer.SanitizeHtml(input);
        result.Should().Contain("Safe paragraph");
        result.Should().Contain("Safe link");
        result.Should().Contain("Bold text");
        result.Should().NotContain("<script");
        result.Should().NotContain("onerror");
        result.Should().NotContain("alert");
    }
}
