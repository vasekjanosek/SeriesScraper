using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Tests.Services;

public class ForumCrudServiceTests
{
    private readonly IForumRepository _forumRepository = Substitute.For<IForumRepository>();
    private readonly IUrlValidator _urlValidator = Substitute.For<IUrlValidator>();
    private readonly ICredentialProtector _credentialProtector = Substitute.For<ICredentialProtector>();
    private readonly ILogger<ForumCrudService> _logger = Substitute.For<ILogger<ForumCrudService>>();
    private readonly ForumCrudService _sut;

    public ForumCrudServiceTests()
    {
        _urlValidator.IsUrlSafe(Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(x =>
            {
                x[1] = null;
                return true;
            });
        _credentialProtector.Encrypt(Arg.Any<string>()).Returns(x => $"ENC({x[0]})");
        _credentialProtector.Decrypt(Arg.Any<string>()).Returns(x => $"DEC({x[0]})");

        _sut = new ForumCrudService(_forumRepository, _urlValidator, _credentialProtector, _logger);
    }

    // --- GetAllForumsAsync ---

    [Fact]
    public async Task GetAllForumsAsync_ReturnsAllForumsMappedToDto()
    {
        var forums = new List<Forum>
        {
            CreateForum(1, "Forum A"),
            CreateForum(2, "Forum B")
        };
        _forumRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(forums.AsReadOnly());

        var result = await _sut.GetAllForumsAsync();

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Forum A");
        result[1].Name.Should().Be("Forum B");
    }

    [Fact]
    public async Task GetAllForumsAsync_NeverExposesEncryptedPassword()
    {
        var forum = CreateForum(1, "Forum A");
        forum.EncryptedPassword = "encrypted-secret";
        _forumRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Forum> { forum }.AsReadOnly());

        var result = await _sut.GetAllForumsAsync();

        result[0].HasPassword.Should().BeTrue();
        // ForumDto has no Password or EncryptedPassword property
        result[0].GetType().GetProperty("Password").Should().BeNull();
        result[0].GetType().GetProperty("EncryptedPassword").Should().BeNull();
    }

    // --- GetForumByIdAsync ---

    [Fact]
    public async Task GetForumByIdAsync_ExistingForum_ReturnsDto()
    {
        var forum = CreateForum(5, "Test Forum");
        _forumRepository.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(forum);

        var result = await _sut.GetForumByIdAsync(5);

        result.Should().NotBeNull();
        result!.ForumId.Should().Be(5);
        result.Name.Should().Be("Test Forum");
    }

    [Fact]
    public async Task GetForumByIdAsync_NonExistentForum_ReturnsNull()
    {
        _forumRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((Forum?)null);

        var result = await _sut.GetForumByIdAsync(999);

        result.Should().BeNull();
    }

    // --- CreateForumAsync ---

    [Fact]
    public async Task CreateForumAsync_ValidInput_CreatesForum()
    {
        var dto = new CreateForumDto
        {
            Name = "New Forum",
            BaseUrl = "https://example.com",
            Username = "user1",
            CredentialKey = "FORUM_EXAMPLE",
            Password = "secret123",
            CrawlDepth = 2,
            PolitenessDelayMs = 1000
        };

        var result = await _sut.CreateForumAsync(dto);

        result.Name.Should().Be("New Forum");
        result.BaseUrl.Should().Be("https://example.com");
        result.HasPassword.Should().BeTrue();
        await _forumRepository.Received(1).AddAsync(
            Arg.Is<Forum>(f => f.Name == "New Forum" && f.EncryptedPassword == "ENC(secret123)"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateForumAsync_NoPassword_CreatesForumWithoutEncryptedPassword()
    {
        var dto = new CreateForumDto
        {
            Name = "No Pass Forum",
            BaseUrl = "https://example.com",
            Username = "user1",
            CredentialKey = "FORUM_NOPASS"
        };

        var result = await _sut.CreateForumAsync(dto);

        result.HasPassword.Should().BeFalse();
        await _forumRepository.Received(1).AddAsync(
            Arg.Is<Forum>(f => f.EncryptedPassword == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateForumAsync_UnsafeUrl_ThrowsArgumentException()
    {
        _urlValidator.IsUrlSafe(Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(x =>
            {
                x[1] = "Host resolves to private IP.";
                return false;
            });

        var dto = new CreateForumDto
        {
            Name = "Bad Forum",
            BaseUrl = "http://192.168.1.1",
            Username = "user1",
            CredentialKey = "FORUM_BAD"
        };

        var act = () => _sut.CreateForumAsync(dto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not valid*");
    }

    [Fact]
    public async Task CreateForumAsync_InvalidCredentialKey_ThrowsArgumentException()
    {
        var dto = new CreateForumDto
        {
            Name = "Bad Key Forum",
            BaseUrl = "https://example.com",
            Username = "user1",
            CredentialKey = "INVALID_KEY"
        };

        var act = () => _sut.CreateForumAsync(dto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*does not match*");
    }

    [Fact]
    public async Task CreateForumAsync_JavaScriptScheme_BlockedByUrlValidator()
    {
        _urlValidator.IsUrlSafe("javascript:alert(1)", out Arg.Any<string?>())
            .Returns(x =>
            {
                x[1] = "Scheme 'javascript' is not allowed.";
                return false;
            });

        var dto = new CreateForumDto
        {
            Name = "XSS Forum",
            BaseUrl = "javascript:alert(1)",
            Username = "user1",
            CredentialKey = "FORUM_XSS"
        };

        var act = () => _sut.CreateForumAsync(dto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not valid*");
    }

    // --- UpdateForumAsync ---

    [Fact]
    public async Task UpdateForumAsync_ValidUpdate_UpdatesForum()
    {
        var existing = CreateForum(1, "Old Name");
        _forumRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(existing);

        var dto = new UpdateForumDto
        {
            Name = "New Name",
            BaseUrl = "https://updated.com",
            Username = "newuser",
            CredentialKey = "FORUM_UPDATED",
            CrawlDepth = 3,
            PolitenessDelayMs = 2000,
            IsActive = true
        };

        var result = await _sut.UpdateForumAsync(1, dto);

        result.Name.Should().Be("New Name");
        result.BaseUrl.Should().Be("https://updated.com");
        await _forumRepository.Received(1).UpdateAsync(
            Arg.Is<Forum>(f => f.Name == "New Name"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateForumAsync_WithNewPassword_EncryptsAndStores()
    {
        var existing = CreateForum(1, "Forum");
        _forumRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(existing);

        var dto = new UpdateForumDto
        {
            Name = "Forum",
            BaseUrl = "https://example.com",
            Username = "user1",
            CredentialKey = "FORUM_EXAMPLE",
            Password = "newpassword",
            CrawlDepth = 1,
            PolitenessDelayMs = 500,
            IsActive = true
        };

        await _sut.UpdateForumAsync(1, dto);

        await _forumRepository.Received(1).UpdateAsync(
            Arg.Is<Forum>(f => f.EncryptedPassword == "ENC(newpassword)"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateForumAsync_BlankPassword_KeepsExistingEncryptedPassword()
    {
        var existing = CreateForum(1, "Forum");
        existing.EncryptedPassword = "previouslyEncrypted";
        _forumRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(existing);

        var dto = new UpdateForumDto
        {
            Name = "Forum",
            BaseUrl = "https://example.com",
            Username = "user1",
            CredentialKey = "FORUM_EXAMPLE",
            Password = null,
            CrawlDepth = 1,
            PolitenessDelayMs = 500,
            IsActive = true
        };

        await _sut.UpdateForumAsync(1, dto);

        await _forumRepository.Received(1).UpdateAsync(
            Arg.Is<Forum>(f => f.EncryptedPassword == "previouslyEncrypted"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateForumAsync_NonExistentForum_ThrowsInvalidOperation()
    {
        _forumRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((Forum?)null);

        var dto = new UpdateForumDto
        {
            Name = "x",
            BaseUrl = "https://example.com",
            Username = "x",
            CredentialKey = "FORUM_X",
            CrawlDepth = 1,
            PolitenessDelayMs = 500,
            IsActive = true
        };

        var act = () => _sut.UpdateForumAsync(999, dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // --- DeleteForumAsync ---

    [Fact]
    public async Task DeleteForumAsync_ExistingForum_DenormalizesAndDeletes()
    {
        var existing = CreateForum(1, "Doomed Forum");
        _forumRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(existing);

        await _sut.DeleteForumAsync(1);

        await _forumRepository.Received(1).DenormalizeForumNameOnRunsAsync(1, "Doomed Forum", Arg.Any<CancellationToken>());
        await _forumRepository.Received(1).DeleteAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteForumAsync_DenormalizesBeforeDeleting()
    {
        var existing = CreateForum(1, "Forum");
        _forumRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(existing);

        var callOrder = new List<string>();
        _forumRepository.DenormalizeForumNameOnRunsAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("denormalize"));
        _forumRepository.DeleteAsync(Arg.Any<Forum>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("delete"));

        await _sut.DeleteForumAsync(1);

        callOrder.Should().ContainInOrder("denormalize", "delete");
    }

    [Fact]
    public async Task DeleteForumAsync_NonExistentForum_ThrowsInvalidOperation()
    {
        _forumRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((Forum?)null);

        var act = () => _sut.DeleteForumAsync(999);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // --- URL Validation Edge Cases ---

    [Theory]
    [InlineData("ftp://files.example.com")]
    [InlineData("file:///etc/passwd")]
    [InlineData("data:text/html,<h1>test</h1>")]
    public async Task CreateForumAsync_BlockedSchemes_Throws(string url)
    {
        _urlValidator.IsUrlSafe(url, out Arg.Any<string?>())
            .Returns(x =>
            {
                x[1] = $"Scheme not allowed.";
                return false;
            });

        var dto = new CreateForumDto
        {
            Name = "Bad Scheme Forum",
            BaseUrl = url,
            Username = "user1",
            CredentialKey = "FORUM_BAD"
        };

        var act = () => _sut.CreateForumAsync(dto);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // --- Helpers ---

    private static Forum CreateForum(int id, string name) => new()
    {
        ForumId = id,
        Name = name,
        BaseUrl = "https://example.com",
        Username = "testuser",
        CredentialKey = "FORUM_TEST",
        CrawlDepth = 1,
        PolitenessDelayMs = 500,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
