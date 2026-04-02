using FluentAssertions;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Tests.ValueObjects;

public class ForumCredentialsTests
{
    [Fact]
    public void ForumCredentials_WithSameValues_AreEqual()
    {
        var a = new ForumCredentials { Username = "user1", Password = "pass1" };
        var b = new ForumCredentials { Username = "user1", Password = "pass1" };

        a.Should().Be(b);
    }

    [Fact]
    public void ForumCredentials_WithDifferentValues_AreNotEqual()
    {
        var a = new ForumCredentials { Username = "user1", Password = "pass1" };
        var b = new ForumCredentials { Username = "user2", Password = "pass1" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void ForumCredentials_StoresUsernameAndPassword()
    {
        var creds = new ForumCredentials { Username = "testuser", Password = "testpass" };

        creds.Username.Should().Be("testuser");
        creds.Password.Should().Be("testpass");
    }
}
