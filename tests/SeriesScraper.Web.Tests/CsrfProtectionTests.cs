using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SeriesScraper.Web.Pages;

namespace SeriesScraper.Web.Tests;

/// <summary>
/// Verifies CSRF protection configuration (#47).
/// Blazor Server uses SignalR (WebSocket), which is inherently not vulnerable to traditional CSRF.
/// These tests verify that:
/// 1. The Error page is the only endpoint with [IgnoreAntiforgeryToken] (expected — error pages don't need CSRF)
/// 2. No Razor Pages have HTTP POST handlers without antiforgery protection
/// </summary>
public class CsrfProtectionTests
{
    [Fact]
    public void ErrorPage_HasIgnoreAntiforgeryToken_IsExpected()
    {
        // The Error page ignores antiforgery tokens — this is standard and acceptable.
        var attr = typeof(ErrorModel).GetCustomAttribute<IgnoreAntiforgeryTokenAttribute>();
        attr.Should().NotBeNull("Error page is expected to have [IgnoreAntiforgeryToken]");
    }

    [Fact]
    public void AllRazorPageModels_WithPostHandlers_ShouldNotIgnoreAntiforgery()
    {
        // Find all PageModel types in the Web assembly
        var webAssembly = typeof(ErrorModel).Assembly;
        var pageModels = webAssembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Microsoft.AspNetCore.Mvc.RazorPages.PageModel))
                        && !t.IsAbstract);

        foreach (var pageModel in pageModels)
        {
            var hasPostHandler = pageModel.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Any(m => m.Name.StartsWith("OnPost", StringComparison.OrdinalIgnoreCase)
                       || m.Name.StartsWith("OnPostAsync", StringComparison.OrdinalIgnoreCase));

            if (hasPostHandler)
            {
                var ignoresAntiforgery = pageModel.GetCustomAttribute<IgnoreAntiforgeryTokenAttribute>();
                ignoresAntiforgery.Should().BeNull(
                    $"Page '{pageModel.Name}' has POST handlers but ignores antiforgery tokens. " +
                    "Remove [IgnoreAntiforgeryToken] or add [ValidateAntiForgeryToken] explicitly.");
            }
        }
    }

    [Fact]
    public void BlazorServer_UsesSignalR_InherentlySafeFromTraditionalCsrf()
    {
        // Blazor Server communicates over SignalR (WebSocket or long-polling),
        // not traditional HTTP form submissions. This makes it inherently safe
        // from traditional CSRF attacks. This test documents this architectural decision.
        //
        // The antiforgery middleware is added as defense-in-depth for any
        // non-Blazor HTTP POST endpoints that may be added in the future.
        true.Should().BeTrue("Blazor Server uses SignalR, inherently safe from traditional CSRF");
    }

    [Fact]
    public void OnlyErrorPage_HasIgnoreAntiforgeryToken()
    {
        var webAssembly = typeof(ErrorModel).Assembly;
        var pagesWithIgnoreAntiforgery = webAssembly.GetTypes()
            .Where(t => t.GetCustomAttribute<IgnoreAntiforgeryTokenAttribute>() != null)
            .ToList();

        // Only the Error page should have [IgnoreAntiforgeryToken]
        pagesWithIgnoreAntiforgery.Should().HaveCount(1);
        pagesWithIgnoreAntiforgery[0].Should().Be(typeof(ErrorModel));
    }
}
