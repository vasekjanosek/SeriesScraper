using FluentAssertions;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Tests.ValueObjects;

public class ForumCredentialsTests
{
    [Fact]
    public void ForumCredentials_StoresUsernameAndPassword()
    {
        var creds = new ForumCredentials { Username = "testuser", Password = "testpass" };

        creds.Username.Should().Be("testuser");
        creds.Password.Should().Be("testpass");
    }

    [Fact]
    public void ForumCredentials_WithSameValues_AreEqual()
    {
        var a = new ForumCredentials { Username = "user1", Password = "pass1" };
        var b = new ForumCredentials { Username = "user1", Password = "pass1" };

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void ForumCredentials_WithDifferentUsername_AreNotEqual()
    {
        var a = new ForumCredentials { Username = "user1", Password = "pass1" };
        var b = new ForumCredentials { Username = "user2", Password = "pass1" };

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void ForumCredentials_WithDifferentPassword_AreNotEqual()
    {
        var a = new ForumCredentials { Username = "user1", Password = "pass1" };
        var b = new ForumCredentials { Username = "user1", Password = "pass2" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void ForumCredentials_GetHashCode_SameForEqualInstances()
    {
        var a = new ForumCredentials { Username = "user1", Password = "pass1" };
        var b = new ForumCredentials { Username = "user1", Password = "pass1" };

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void ForumCredentials_GetHashCode_DiffersForDifferentInstances()
    {
        var a = new ForumCredentials { Username = "user1", Password = "pass1" };
        var b = new ForumCredentials { Username = "user2", Password = "pass2" };

        a.GetHashCode().Should().NotBe(b.GetHashCode());
    }

    [Fact]
    public void ForumCredentials_ToString_ContainsTypeName()
    {
        var creds = new ForumCredentials { Username = "testuser", Password = "testpass" };

        creds.ToString().Should().Contain("ForumCredentials");
        creds.ToString().Should().Contain("testuser");
    }

    [Fact]
    public void ForumCredentials_WithExpression_CreatesModifiedCopy()
    {
        var original = new ForumCredentials { Username = "user1", Password = "pass1" };
        var modified = original with { Password = "newpass" };

        modified.Username.Should().Be("user1");
        modified.Password.Should().Be("newpass");
        original.Password.Should().Be("pass1");
    }
}
