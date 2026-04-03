using FluentAssertions;
using SeriesScraper.Application.Services;

namespace SeriesScraper.Application.Tests.Services;

public class PhpBB2ResponseValidatorTests
{
    private readonly PhpBB2ResponseValidator _sut = new();

    // --- Positive: login page detection ---

    [Theory]
    [InlineData("<html><body><form action='login.php' method='post'><input name='username'/></form></body></html>")]
    [InlineData("<html><body><form action=\"login.php\" method=\"post\"><input name=\"username\"/></form></body></html>")]
    [InlineData("<html><body><form action='./login.php' method='post'></form></body></html>")]
    [InlineData("<html><body><form action=\"./login.php\" method=\"post\"></form></body></html>")]
    [InlineData("<html><body><form name='login' method='post'></form></body></html>")]
    [InlineData("<html><body><form name=\"login\" method=\"post\"></form></body></html>")]
    [InlineData("<html><body><form id='login' method='post'></form></body></html>")]
    [InlineData("<html><body><form id=\"login\" method=\"post\"></form></body></html>")]
    public void IsSessionExpired_LoginPageHtml_ReturnsTrue(string html)
    {
        _sut.IsSessionExpired(html).Should().BeTrue();
    }

    [Fact]
    public void IsSessionExpired_CaseInsensitive_ReturnsTrue()
    {
        var html = "<html><body><FORM ACTION='LOGIN.PHP' METHOD='POST'></FORM></body></html>";
        _sut.IsSessionExpired(html).Should().BeTrue();
    }

    [Fact]
    public void IsSessionExpired_RealPhpBB2LoginPage_ReturnsTrue()
    {
        var html = """
            <html>
            <head><title>Forum - Login</title></head>
            <body>
            <h1>Login</h1>
            <form action="login.php" method="post">
                <input type="text" name="username" />
                <input type="password" name="password" />
                <input type="submit" value="Log in" />
            </form>
            </body>
            </html>
            """;
        _sut.IsSessionExpired(html).Should().BeTrue();
    }

    // --- Negative: normal page content ---

    [Theory]
    [InlineData("<html><body><h1>Forum Thread</h1><div class='post'>Hello world</div></body></html>")]
    [InlineData("<html><body><table><tr><td>Post content goes here</td></tr></table></body></html>")]
    [InlineData("<html><body>Simple text content</body></html>")]
    public void IsSessionExpired_NormalPageContent_ReturnsFalse(string html)
    {
        _sut.IsSessionExpired(html).Should().BeFalse();
    }

    [Fact]
    public void IsSessionExpired_NullInput_ReturnsFalse()
    {
        _sut.IsSessionExpired(null!).Should().BeFalse();
    }

    [Fact]
    public void IsSessionExpired_EmptyInput_ReturnsFalse()
    {
        _sut.IsSessionExpired("").Should().BeFalse();
    }

    [Fact]
    public void IsSessionExpired_WhitespaceInput_ReturnsFalse()
    {
        _sut.IsSessionExpired("   ").Should().BeFalse();
    }

    [Fact]
    public void IsSessionExpired_LoginTextWithoutForm_ReturnsFalse()
    {
        // A page that mentions login.php in text but has no <form> element
        var html = "<html><body><p>Please visit login.php to log in. name='login'</p></body></html>";
        _sut.IsSessionExpired(html).Should().BeFalse();
    }

    [Fact]
    public void IsSessionExpired_PageWithLoginLinkInSidebar_ReturnsFalse()
    {
        // A normal page that has a login link in a sidebar/nav, but the main content is valid
        // This should NOT trigger false positive because there's no form with login.php action
        var html = """
            <html>
            <body>
            <div class="sidebar"><a href="login.php">Login</a></div>
            <div class="content">
                <h1>Thread: Best Movies 2024</h1>
                <div class="post">Download links here</div>
            </div>
            </body>
            </html>
            """;
        _sut.IsSessionExpired(html).Should().BeFalse();
    }

    [Fact]
    public void IsSessionExpired_FormWithDifferentAction_ReturnsFalse()
    {
        var html = "<html><body><form action='search.php' method='post'><input name='query'/></form></body></html>";
        _sut.IsSessionExpired(html).Should().BeFalse();
    }
}
