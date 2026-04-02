using FluentAssertions;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Tests.ValueObjects;

public class CredentialKeyTests
{
    [Theory]
    [InlineData("FORUM_PASSWORD")]
    [InlineData("FORUM_KINOMANIA_PASSWORD")]
    [InlineData("FORUM_A")]
    [InlineData("FORUM_123")]
    [InlineData("FORUM_ABC_DEF_123")]
    [InlineData("FORUM_A1B2C3")]
    public void Constructor_ValidKey_CreatesInstance(string key)
    {
        var credentialKey = new CredentialKey(key);

        credentialKey.Value.Should().Be(key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_NullOrWhitespace_ThrowsArgumentException(string? key)
    {
        var act = () => new CredentialKey(key!);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("value");
    }

    [Theory]
    [InlineData("DB_PASSWORD")]
    [InlineData("GITHUB_TOKEN")]
    [InlineData("forum_password")]
    [InlineData("FORUM_")]
    [InlineData("FORUM")]
    [InlineData("FORUM_lowercase")]
    [InlineData("FORUM_has space")]
    [InlineData("FORUM_SPECIAL!CHAR")]
    [InlineData("PASSWORD")]
    [InlineData("FORUM_key-with-dash")]
    public void Constructor_InvalidKey_ThrowsArgumentException(string key)
    {
        var act = () => new CredentialKey(key);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("value")
            .WithMessage($"*'{key}'*does not match*");
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var key = new CredentialKey("FORUM_TEST_KEY");

        key.ToString().Should().Be("FORUM_TEST_KEY");
    }

    [Fact]
    public void ImplicitConversion_ReturnsValue()
    {
        var key = new CredentialKey("FORUM_CONVERT");

        string result = key;

        result.Should().Be("FORUM_CONVERT");
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = new CredentialKey("FORUM_SAME_KEY");
        var b = new CredentialKey("FORUM_SAME_KEY");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        var a = new CredentialKey("FORUM_KEY_A");
        var b = new CredentialKey("FORUM_KEY_B");

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_SameValue_SameHash()
    {
        var a = new CredentialKey("FORUM_HASH_TEST");
        var b = new CredentialKey("FORUM_HASH_TEST");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValue_DifferentHash()
    {
        var a = new CredentialKey("FORUM_HASH_A");
        var b = new CredentialKey("FORUM_HASH_B");

        a.GetHashCode().Should().NotBe(b.GetHashCode());
    }

    [Fact]
    public void Pattern_IsCorrectRegex()
    {
        CredentialKey.Pattern.Should().Be(@"^FORUM_[A-Z0-9_]+$");
    }
}
