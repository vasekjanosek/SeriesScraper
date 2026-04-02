using System.Text.RegularExpressions;
using FluentAssertions;
using SeriesScraper.Application.Security;

namespace SeriesScraper.Application.Tests.Services;

public class SafeRegexTests
{
    [Fact]
    public void Create_ReturnsRegexWithTimeout()
    {
        var regex = SafeRegex.Create(@"\d+");
        regex.MatchTimeout.Should().Be(SafeRegex.DefaultTimeout);
    }

    [Fact]
    public void Create_WithSmallerTimeout_UsesProvidedTimeout()
    {
        var timeout = TimeSpan.FromMilliseconds(500);
        var regex = SafeRegex.Create(@"\d+", timeout: timeout);
        regex.MatchTimeout.Should().Be(timeout);
    }

    [Fact]
    public void Create_WithLargerTimeout_CapsAtDefault()
    {
        var timeout = TimeSpan.FromSeconds(10);
        var regex = SafeRegex.Create(@"\d+", timeout: timeout);
        regex.MatchTimeout.Should().Be(SafeRegex.DefaultTimeout);
    }

    [Fact]
    public void Create_WithOptions_PassesOptionsThrough()
    {
        var regex = SafeRegex.Create(@"hello", RegexOptions.IgnoreCase);
        regex.Options.Should().HaveFlag(RegexOptions.IgnoreCase);
    }

    [Fact]
    public void SafeMatch_MatchFound_ReturnsMatch()
    {
        var match = SafeRegex.SafeMatch("abc123def", @"\d+");
        match.Should().NotBeNull();
        match!.Value.Should().Be("123");
    }

    [Fact]
    public void SafeMatch_NoMatch_ReturnsNull()
    {
        var match = SafeRegex.SafeMatch("abcdef", @"\d+");
        match.Should().BeNull();
    }

    [Fact]
    public void SafeMatch_CatastrophicBacktracking_ReturnsNullInsteadOfThrowing()
    {
        // Pattern with catastrophic backtracking potential
        var evilPattern = @"^(a+)+$";
        var evilInput = new string('a', 30) + "!";

        // Should return null (timeout) rather than throw
        var match = SafeRegex.SafeMatch(evilInput, evilPattern);
        match.Should().BeNull();
    }

    [Fact]
    public void SafeIsMatch_Matches_ReturnsTrue()
    {
        SafeRegex.SafeIsMatch("abc123", @"\d+").Should().BeTrue();
    }

    [Fact]
    public void SafeIsMatch_NoMatch_ReturnsFalse()
    {
        SafeRegex.SafeIsMatch("abcdef", @"\d+").Should().BeFalse();
    }

    [Fact]
    public void SafeIsMatch_CatastrophicBacktracking_ReturnsFalseInsteadOfThrowing()
    {
        var evilPattern = @"^(a+)+$";
        var evilInput = new string('a', 30) + "!";

        SafeRegex.SafeIsMatch(evilInput, evilPattern).Should().BeFalse();
    }

    [Fact]
    public void DefaultTimeout_IsTwoSeconds()
    {
        SafeRegex.DefaultTimeout.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SetGlobalTimeout_SetsAppDomainData()
    {
        SafeRegex.SetGlobalTimeout();

        var timeout = AppDomain.CurrentDomain.GetData("REGEX_DEFAULT_MATCH_TIMEOUT");
        timeout.Should().Be(SafeRegex.DefaultTimeout);
    }

    [Fact]
    public void SetGlobalTimeout_ValueCanBeRetrieved()
    {
        SafeRegex.SetGlobalTimeout();

        // Verify the AppDomain data is set correctly — the .NET runtime
        // will use this as the default timeout for new Regex instances
        // created without an explicit timeout parameter.
        var timeout = AppDomain.CurrentDomain.GetData("REGEX_DEFAULT_MATCH_TIMEOUT") as TimeSpan?;
        timeout.Should().NotBeNull();
        timeout!.Value.Should().Be(SafeRegex.DefaultTimeout);
    }

    [Fact]
    public void Create_CatastrophicPattern_TimesOutOnExecution()
    {
        var regex = SafeRegex.Create(@"^(a+)+$");
        var evilInput = new string('a', 30) + "!";

        var act = () => regex.Match(evilInput);
        act.Should().Throw<RegexMatchTimeoutException>();
    }
}
