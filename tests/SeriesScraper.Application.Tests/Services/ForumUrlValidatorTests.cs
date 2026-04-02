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
}
