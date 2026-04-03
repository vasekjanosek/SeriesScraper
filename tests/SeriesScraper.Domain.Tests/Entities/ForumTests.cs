using FluentAssertions;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Domain.Tests.Entities;

public class ForumTests
{
    [Theory]
    [InlineData("FORUM_PASSWORD")]
    [InlineData("FORUM_WARFORUM_PASSWORD")]
    [InlineData("FORUM_A1B2C3")]
    public void CredentialKey_ValidPattern_SetsValue(string key)
    {
        var forum = new Forum
        {
            Name = "Test",
            BaseUrl = "https://example.com",
            Username = "user",
            CredentialKey = key
        };

        forum.CredentialKey.Should().Be(key);
    }

    [Theory]
    [InlineData("DB_PASSWORD")]
    [InlineData("GITHUB_TOKEN")]
    [InlineData("PATH")]
    [InlineData("HOME")]
    [InlineData("SECRET_KEY")]
    [InlineData("forum_password")]
    [InlineData("FORUM_")]
    [InlineData("")]
    public void CredentialKey_InvalidPattern_ThrowsArgumentException(string key)
    {
        var act = () => new Forum
        {
            Name = "Test",
            BaseUrl = "https://example.com",
            Username = "user",
            CredentialKey = key
        };

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CredentialKey_Null_ThrowsArgumentException()
    {
        var act = () => new Forum
        {
            Name = "Test",
            BaseUrl = "https://example.com",
            Username = "user",
            CredentialKey = null!
        };

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CredentialKey_UpdateToInvalid_ThrowsArgumentException()
    {
        var forum = new Forum
        {
            Name = "Test",
            BaseUrl = "https://example.com",
            Username = "user",
            CredentialKey = "FORUM_VALID_KEY"
        };

        var act = () => forum.CredentialKey = "DB_PASSWORD";

        act.Should().Throw<ArgumentException>();
        forum.CredentialKey.Should().Be("FORUM_VALID_KEY");
    }

    [Fact]
    public void CredentialKey_UpdateToValid_Succeeds()
    {
        var forum = new Forum
        {
            Name = "Test",
            BaseUrl = "https://example.com",
            Username = "user",
            CredentialKey = "FORUM_OLD_KEY"
        };

        forum.CredentialKey = "FORUM_NEW_KEY";

        forum.CredentialKey.Should().Be("FORUM_NEW_KEY");
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var forum = new Forum
        {
            Name = "Test",
            BaseUrl = "https://example.com",
            Username = "user",
            CredentialKey = "FORUM_PASSWORD"
        };

        forum.CrawlDepth.Should().Be(1);
        forum.PolitenessDelayMs.Should().Be(500);
        forum.IsActive.Should().BeTrue();
        forum.Sections.Should().BeEmpty();
        forum.ScrapeRuns.Should().BeEmpty();
    }
}
