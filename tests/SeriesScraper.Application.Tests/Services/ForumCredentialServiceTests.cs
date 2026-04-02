using FluentAssertions;
using SeriesScraper.Application.Services;

namespace SeriesScraper.Application.Tests.Services;

public class ForumCredentialServiceTests : IDisposable
{
    private readonly ForumCredentialService _sut = new();
    private readonly List<string> _envVarsToClean = new();

    private void SetEnvVar(string key, string value)
    {
        Environment.SetEnvironmentVariable(key, value);
        _envVarsToClean.Add(key);
    }

    public void Dispose()
    {
        foreach (var key in _envVarsToClean)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    // ── ResolveCredential ──────────────────────────────────────────────────

    [Fact]
    public void ResolveCredential_ValidKeyWithValue_ReturnsValue()
    {
        SetEnvVar("FORUM_TEST_PASSWORD", "secret123");

        var result = _sut.ResolveCredential("FORUM_TEST_PASSWORD");

        result.Should().Be("secret123");
    }

    [Fact]
    public void ResolveCredential_ValidKeyNotSet_ReturnsNull()
    {
        var result = _sut.ResolveCredential("FORUM_NONEXISTENT_KEY");

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveCredential_InvalidKeyFormat_ThrowsArgumentException()
    {
        var act = () => _sut.ResolveCredential("DB_PASSWORD");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ResolveCredential_NullKey_ThrowsArgumentException()
    {
        var act = () => _sut.ResolveCredential(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ResolveCredential_EmptyKey_ThrowsArgumentException()
    {
        var act = () => _sut.ResolveCredential("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ResolveCredential_LowercaseForumKey_ThrowsArgumentException()
    {
        var act = () => _sut.ResolveCredential("forum_password");

        act.Should().Throw<ArgumentException>();
    }

    // ── ValidateActiveForumCredentials ──────────────────────────────────────

    [Fact]
    public void ValidateActiveForumCredentials_AllSet_ReturnsEmpty()
    {
        SetEnvVar("FORUM_SITE_A_PASS", "pass1");
        SetEnvVar("FORUM_SITE_B_PASS", "pass2");

        var forums = new List<(string, string, bool)>
        {
            ("Site A", "FORUM_SITE_A_PASS", true),
            ("Site B", "FORUM_SITE_B_PASS", true),
        };

        var result = _sut.ValidateActiveForumCredentials(forums);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ValidateActiveForumCredentials_OneMissing_ReturnsMissing()
    {
        SetEnvVar("FORUM_SITE_A_PASS", "pass1");
        // FORUM_SITE_B_PASS is NOT set

        var forums = new List<(string, string, bool)>
        {
            ("Site A", "FORUM_SITE_A_PASS", true),
            ("Site B", "FORUM_SITE_B_PASS", true),
        };

        var result = _sut.ValidateActiveForumCredentials(forums);

        result.Should().HaveCount(1);
        result[0].ForumName.Should().Be("Site B");
        result[0].CredentialKey.Should().Be("FORUM_SITE_B_PASS");
    }

    [Fact]
    public void ValidateActiveForumCredentials_InactiveForumSkipped()
    {
        // FORUM_INACTIVE_PASS is NOT set, but the forum is inactive
        var forums = new List<(string, string, bool)>
        {
            ("Inactive Forum", "FORUM_INACTIVE_PASS", false),
        };

        var result = _sut.ValidateActiveForumCredentials(forums);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ValidateActiveForumCredentials_EmptyList_ReturnsEmpty()
    {
        var forums = new List<(string, string, bool)>();

        var result = _sut.ValidateActiveForumCredentials(forums);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ValidateActiveForumCredentials_MixedActiveInactive_OnlyChecksActive()
    {
        SetEnvVar("FORUM_ACTIVE_PASS", "pass");
        // FORUM_MISSING_ACTIVE is NOT set
        // FORUM_MISSING_INACTIVE is NOT set but forum is inactive

        var forums = new List<(string, string, bool)>
        {
            ("Active Set", "FORUM_ACTIVE_PASS", true),
            ("Active Missing", "FORUM_MISSING_ACTIVE", true),
            ("Inactive Missing", "FORUM_MISSING_INACTIVE", false),
        };

        var result = _sut.ValidateActiveForumCredentials(forums);

        result.Should().HaveCount(1);
        result[0].ForumName.Should().Be("Active Missing");
    }

    [Fact]
    public void ValidateActiveForumCredentials_EmptyValueTreatedAsMissing()
    {
        SetEnvVar("FORUM_EMPTY_VAL", "");

        var forums = new List<(string, string, bool)>
        {
            ("Empty Forum", "FORUM_EMPTY_VAL", true),
        };

        var result = _sut.ValidateActiveForumCredentials(forums);

        result.Should().HaveCount(1);
        result[0].ForumName.Should().Be("Empty Forum");
    }
}
