using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SeriesScraper.Application.Services;

namespace SeriesScraper.Application.Tests.Services;

public class ForumUrlValidatorTests
{
    private readonly ForumUrlValidator _validator;

    public ForumUrlValidatorTests()
    {
        var logger = Substitute.For<ILogger<ForumUrlValidator>>();
        _validator = new ForumUrlValidator(logger);
    }

    // --- Valid URLs ---

    [Theory]
    [InlineData("https://forum.example.com")]
    [InlineData("http://forum.example.com")]
    [InlineData("https://www.example.com/forum/index.php")]
    [InlineData("https://example.com:8080/path")]
    public void IsUrlSafe_PublicHttpUrl_ReturnsTrue(string url)
    {
        _validator.IsUrlSafe(url).Should().BeTrue();
    }

    // --- Blocked Schemes ---

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("file:///C:/Windows/System32/config/sam")]
    [InlineData("ftp://ftp.example.com")]
    [InlineData("gopher://example.com")]
    [InlineData("dict://example.com")]
    [InlineData("sftp://example.com")]
    [InlineData("ldap://example.com")]
    public void IsUrlSafe_BlockedScheme_ReturnsFalse(string url)
    {
        _validator.IsUrlSafe(url, out var reason).Should().BeFalse();
        reason.Should().Contain("not allowed");
    }

    // --- Loopback ---

    [Theory]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("http://127.1.2.3")]
    [InlineData("https://127.255.255.255")]
    public void IsUrlSafe_Loopback127_ReturnsFalse(string url)
    {
        _validator.IsUrlSafe(url, out var reason).Should().BeFalse();
        reason.Should().Contain("blocked");
    }

    [Theory]
    [InlineData("http://localhost")]
    [InlineData("http://localhost:3000")]
    [InlineData("https://localhost")]
    [InlineData("http://localhost.localdomain")]
    public void IsUrlSafe_Localhost_ReturnsFalse(string url)
    {
        _validator.IsUrlSafe(url, out var reason).Should().BeFalse();
        reason.Should().Contain("localhost");
    }

    // --- Private IP Ranges ---

    [Theory]
    [InlineData("http://10.0.0.1")]
    [InlineData("http://10.255.255.255")]
    [InlineData("http://10.0.0.1:8080/path")]
    public void IsUrlSafe_PrivateRange10_ReturnsFalse(string url)
    {
        _validator.IsUrlSafe(url, out var reason).Should().BeFalse();
        reason.Should().Contain("blocked");
    }

    [Theory]
    [InlineData("http://172.16.0.1")]
    [InlineData("http://172.31.255.255")]
    [InlineData("http://172.20.0.1")]
    public void IsUrlSafe_PrivateRange172_ReturnsFalse(string url)
    {
        _validator.IsUrlSafe(url, out var reason).Should().BeFalse();
        reason.Should().Contain("blocked");
    }

    [Theory]
    [InlineData("http://192.168.0.1")]
    [InlineData("http://192.168.1.1")]
    [InlineData("http://192.168.255.255")]
    public void IsUrlSafe_PrivateRange192_ReturnsFalse(string url)
    {
        _validator.IsUrlSafe(url, out var reason).Should().BeFalse();
        reason.Should().Contain("blocked");
    }

    // --- Edge boundaries for 172.16.0.0/12 ---

    [Fact]
    public void IsUrlSafe_172_15_NotBlocked_BoundaryBelow()
    {
        // 172.15.x.x is NOT in the private range
        _validator.IsUrlSafe("http://172.15.0.1").Should().BeTrue();
    }

    [Fact]
    public void IsUrlSafe_172_32_NotBlocked_BoundaryAbove()
    {
        // 172.32.x.x is NOT in the private range
        _validator.IsUrlSafe("http://172.32.0.1").Should().BeTrue();
    }

    // --- Other blocked ranges ---

    [Theory]
    [InlineData("http://169.254.1.1")]
    [InlineData("http://169.254.169.254")]  // AWS metadata endpoint
    public void IsUrlSafe_LinkLocal_ReturnsFalse(string url)
    {
        _validator.IsUrlSafe(url, out var reason).Should().BeFalse();
        reason.Should().Contain("blocked");
    }

    [Theory]
    [InlineData("http://0.0.0.0")]
    [InlineData("http://0.0.0.1")]
    public void IsUrlSafe_ZeroRange_ReturnsFalse(string url)
    {
        _validator.IsUrlSafe(url, out var reason).Should().BeFalse();
        reason.Should().Contain("blocked");
    }

    [Theory]
    [InlineData("http://100.64.0.1")]
    [InlineData("http://100.127.255.255")]
    public void IsUrlSafe_CarrierGradeNat_ReturnsFalse(string url)
    {
        _validator.IsUrlSafe(url, out var reason).Should().BeFalse();
        reason.Should().Contain("blocked");
    }

    [Fact]
    public void IsUrlSafe_MulticastRange_ReturnsFalse()
    {
        _validator.IsUrlSafe("http://224.0.0.1", out var reason).Should().BeFalse();
        reason.Should().Contain("blocked");
    }

    [Fact]
    public void IsUrlSafe_ReservedRange_ReturnsFalse()
    {
        _validator.IsUrlSafe("http://240.0.0.1", out var reason).Should().BeFalse();
        reason.Should().Contain("blocked");
    }

    [Fact]
    public void IsUrlSafe_BenchmarkRange_ReturnsFalse()
    {
        _validator.IsUrlSafe("http://198.18.0.1", out var reason).Should().BeFalse();
        reason.Should().Contain("blocked");
    }

    // --- Invalid inputs ---

    [Fact]
    public void IsUrlSafe_NullUrl_ReturnsFalse()
    {
        _validator.IsUrlSafe(null!, out var reason).Should().BeFalse();
        reason.Should().Contain("empty");
    }

    [Fact]
    public void IsUrlSafe_EmptyString_ReturnsFalse()
    {
        _validator.IsUrlSafe("", out var reason).Should().BeFalse();
        reason.Should().Contain("empty");
    }

    [Fact]
    public void IsUrlSafe_WhitespaceOnly_ReturnsFalse()
    {
        _validator.IsUrlSafe("   ", out var reason).Should().BeFalse();
        reason.Should().Contain("empty");
    }

    [Fact]
    public void IsUrlSafe_RelativeUrl_ReturnsFalse()
    {
        _validator.IsUrlSafe("/path/to/resource", out var reason).Should().BeFalse();
        reason.Should().Contain("not a valid absolute URI");
    }

    [Fact]
    public void IsUrlSafe_MalformedUrl_ReturnsFalse()
    {
        _validator.IsUrlSafe("not-a-url", out var reason).Should().BeFalse();
        reason.Should().Contain("not a valid absolute URI");
    }

    // --- IPv6 ---

    [Fact]
    public void IsUrlSafe_IPv6Loopback_ReturnsFalse()
    {
        _validator.IsUrlSafe("http://[::1]", out var reason).Should().BeFalse();
        reason.Should().Contain("blocked");
    }

    // --- Static helper tests ---

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.0.0.1", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("192.168.1.1", true)]
    [InlineData("169.254.169.254", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("1.1.1.1", false)]
    [InlineData("93.184.216.34", false)]
    public void IsBlockedIpAddress_ReturnsCorrectResult(string ip, bool expectedBlocked)
    {
        var address = System.Net.IPAddress.Parse(ip);
        ForumUrlValidator.IsBlockedIpAddress(address).Should().Be(expectedBlocked);
    }

    // --- Overload without reason ---

    [Fact]
    public void IsUrlSafe_SimpleOverload_ReturnsSameResult()
    {
        _validator.IsUrlSafe("http://127.0.0.1").Should().BeFalse();
        _validator.IsUrlSafe("https://example.com").Should().BeTrue();
    }

    // --- Malformed URL edge cases ---

    [Theory]
    [InlineData("http://")]
    [InlineData("https://")]
    [InlineData("://missing-scheme.com")]
    [InlineData("htp://typo-scheme.com")]
    public void IsUrlSafe_MalformedUrl_MissingOrBadScheme_ReturnsFalse(string url)
    {
        _validator.IsUrlSafe(url, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("http://example .com")]
    [InlineData("http://exam ple.com/path")]
    public void IsUrlSafe_UrlWithSpaces_ReturnsFalse(string url)
    {
        _validator.IsUrlSafe(url, out _).Should().BeFalse();
    }

    // --- Protocol edge cases ---

    [Theory]
    [InlineData("data:text/html,<h1>Hi</h1>")]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:user@example.com")]
    public void IsUrlSafe_DangerousSchemes_ReturnsFalse(string url)
    {
        _validator.IsUrlSafe(url, out var reason).Should().BeFalse();
        reason.Should().Contain("not allowed");
    }

    // --- URL with credentials (user:pass@host) ---

    [Fact]
    public void IsUrlSafe_UrlWithCredentials_PublicHost_ReturnsTrue()
    {
        _validator.IsUrlSafe("https://user:pass@forum.example.com/path").Should().BeTrue();
    }

    [Fact]
    public void IsUrlSafe_UrlWithCredentials_PrivateHost_ReturnsFalse()
    {
        _validator.IsUrlSafe("https://admin:secret@192.168.1.1/admin", out var reason).Should().BeFalse();
        reason.Should().Contain("blocked");
    }

    // --- Very long URLs ---

    [Fact]
    public void IsUrlSafe_VeryLongUrl_PublicHost_ReturnsTrue()
    {
        var longPath = new string('a', 2000);
        _validator.IsUrlSafe($"https://forum.example.com/{longPath}").Should().BeTrue();
    }

    // --- Unicode / IDN domain names ---

    [Fact]
    public void IsUrlSafe_UnicodeDomain_ReturnsTrue()
    {
        // IDN domains are parsed by Uri and should be treated as valid public domains
        _validator.IsUrlSafe("https://fórum.example.com/path").Should().BeTrue();
    }

    // --- Boundary IP addresses for 172.16.0.0/12 ---

    [Theory]
    [InlineData("172.15.255.255", false)]   // Just below range — allowed
    [InlineData("172.16.0.0", true)]        // First address in range — blocked
    [InlineData("172.31.255.255", true)]    // Last address in range — blocked
    [InlineData("172.32.0.0", false)]       // Just above range — allowed
    public void IsBlockedIpAddress_172Range_BoundaryCheck(string ip, bool expectedBlocked)
    {
        var address = System.Net.IPAddress.Parse(ip);
        ForumUrlValidator.IsBlockedIpAddress(address).Should().Be(expectedBlocked);
    }

    // --- IPv4-mapped IPv6 addresses ---

    [Theory]
    [InlineData("::ffff:10.0.0.1", true)]      // Private 10.x mapped to IPv6
    [InlineData("::ffff:192.168.1.1", true)]    // Private 192.168.x mapped to IPv6
    [InlineData("::ffff:8.8.8.8", false)]       // Public IP mapped to IPv6
    public void IsBlockedIpAddress_IPv4MappedToIPv6_ReturnsCorrectResult(string ip, bool expectedBlocked)
    {
        var address = System.Net.IPAddress.Parse(ip);
        ForumUrlValidator.IsBlockedIpAddress(address).Should().Be(expectedBlocked);
    }

    // --- IPv6 link-local (fe80::/10) ---

    [Theory]
    [InlineData("fe80::1")]
    [InlineData("fe80::abcd:1234")]
    public void IsBlockedIpAddress_IPv6LinkLocal_ReturnsTrue(string ip)
    {
        var address = System.Net.IPAddress.Parse(ip);
        ForumUrlValidator.IsBlockedIpAddress(address).Should().BeTrue();
    }

    // --- IPv6 site-local (fec0::/10) ---

    [Theory]
    [InlineData("fec0::1")]
    [InlineData("fec0::abcd")]
    public void IsBlockedIpAddress_IPv6SiteLocal_ReturnsTrue(string ip)
    {
        var address = System.Net.IPAddress.Parse(ip);
        ForumUrlValidator.IsBlockedIpAddress(address).Should().BeTrue();
    }

    // --- IPv6 unique local (fc00::/7) ---

    [Theory]
    [InlineData("fc00::1")]
    [InlineData("fd00::1")]
    [InlineData("fdab::1234")]
    public void IsBlockedIpAddress_IPv6UniqueLocal_ReturnsTrue(string ip)
    {
        var address = System.Net.IPAddress.Parse(ip);
        ForumUrlValidator.IsBlockedIpAddress(address).Should().BeTrue();
    }

    // --- IPv6 global address (not blocked) ---

    [Theory]
    [InlineData("2001:db8::1")]
    [InlineData("2607:f8b0:4004:800::200e")]
    public void IsBlockedIpAddress_IPv6GlobalUnicast_ReturnsFalse(string ip)
    {
        var address = System.Net.IPAddress.Parse(ip);
        ForumUrlValidator.IsBlockedIpAddress(address).Should().BeFalse();
    }

    // --- 198.18-19 benchmarking range boundary ---

    [Theory]
    [InlineData("198.18.0.1", true)]
    [InlineData("198.19.255.255", true)]
    [InlineData("198.17.255.255", false)]
    [InlineData("198.20.0.0", false)]
    public void IsBlockedIpAddress_BenchmarkRange_BoundaryCheck(string ip, bool expectedBlocked)
    {
        var address = System.Net.IPAddress.Parse(ip);
        ForumUrlValidator.IsBlockedIpAddress(address).Should().Be(expectedBlocked);
    }

    // --- IsUrlSafe with IPv6 in URL form ---

    [Fact]
    public void IsUrlSafe_IPv6LinkLocal_InUrl_ReturnsFalse()
    {
        _validator.IsUrlSafe("http://[fe80::1]", out var reason).Should().BeFalse();
        reason.Should().Contain("blocked");
    }

    [Fact]
    public void IsUrlSafe_IPv6UniqueLocal_InUrl_ReturnsFalse()
    {
        _validator.IsUrlSafe("http://[fd00::1]", out var reason).Should().BeFalse();
        reason.Should().Contain("blocked");
    }
}
