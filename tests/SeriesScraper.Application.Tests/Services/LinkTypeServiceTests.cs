using FluentAssertions;
using Moq;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Tests.Services;

public class LinkTypeServiceTests
{
    private readonly Mock<ILinkTypeRepository> _repoMock = new();
    private readonly LinkTypeService _sut;

    public LinkTypeServiceTests()
    {
        _sut = new LinkTypeService(_repoMock.Object);
    }

    // ── GetAllAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsAllFromRepository()
    {
        var expected = new List<LinkType>
        {
            new() { LinkTypeId = 1, Name = "Direct HTTP", UrlPattern = @"^https?://", IsSystem = true },
            new() { LinkTypeId = 2, Name = "Torrent File", UrlPattern = @"\.torrent$", IsSystem = true }
        };
        _repoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.GetAllAsync();

        result.Should().BeEquivalentTo(expected);
    }

    // ── GetActiveAsync ───────────────────────────────────────

    [Fact]
    public async Task GetActiveAsync_ReturnsActiveFromRepository()
    {
        var expected = new List<LinkType>
        {
            new() { LinkTypeId = 1, Name = "Direct HTTP", UrlPattern = @"^https?://", IsSystem = true, IsActive = true }
        };
        _repoMock.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.GetActiveAsync();

        result.Should().BeEquivalentTo(expected);
    }

    // ── GetByIdAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsLinkType()
    {
        var expected = new LinkType { LinkTypeId = 1, Name = "Test", UrlPattern = ".*" };
        _repoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.GetByIdAsync(1);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LinkType?)null);

        var result = await _sut.GetByIdAsync(999);

        result.Should().BeNull();
    }

    // ── CreateAsync ──────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidInput_CreatesNonSystemLinkType()
    {
        _repoMock.Setup(r => r.ExistsByNameAsync("Custom Type", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repoMock.Setup(r => r.AddAsync(It.IsAny<LinkType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LinkType lt, CancellationToken _) => { lt.LinkTypeId = 5; return lt; });

        var result = await _sut.CreateAsync("Custom Type", @"^ftp://", "fa-download");

        result.Name.Should().Be("Custom Type");
        result.UrlPattern.Should().Be(@"^ftp://");
        result.IconClass.Should().Be("fa-download");
        result.IsSystem.Should().BeFalse();
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ThrowsInvalidOperation()
    {
        _repoMock.Setup(r => r.ExistsByNameAsync("Direct HTTP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => _sut.CreateAsync("Direct HTTP", @"^https?://");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_EmptyName_ThrowsArgumentException(string? name)
    {
        var act = () => _sut.CreateAsync(name!, @"^https?://");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_EmptyPattern_ThrowsArgumentException(string? pattern)
    {
        var act = () => _sut.CreateAsync("Test", pattern!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_InvalidRegex_ThrowsArgumentException()
    {
        _repoMock.Setup(r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => _sut.CreateAsync("Bad Pattern", @"[invalid");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not a valid regex*");
    }

    // ── UpdateAsync ──────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ValidInput_UpdatesLinkType()
    {
        var existing = new LinkType
        {
            LinkTypeId = 5, Name = "Old Name", UrlPattern = @"^old://", IsSystem = false, IsActive = true
        };
        _repoMock.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoMock.Setup(r => r.GetByNameAsync("New Name", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LinkType?)null);

        var result = await _sut.UpdateAsync(5, "New Name", @"^new://", false, "fa-link");

        result.Name.Should().Be("New Name");
        result.UrlPattern.Should().Be(@"^new://");
        result.IsActive.Should().BeFalse();
        result.IconClass.Should().Be("fa-link");
        _repoMock.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NonExistingId_ThrowsKeyNotFound()
    {
        _repoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LinkType?)null);

        var act = () => _sut.UpdateAsync(999, "Name", @"^x://", true);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_DuplicateName_ThrowsInvalidOperation()
    {
        var existing = new LinkType { LinkTypeId = 5, Name = "Original", UrlPattern = ".*" };
        var conflict = new LinkType { LinkTypeId = 6, Name = "Taken", UrlPattern = ".*" };

        _repoMock.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _repoMock.Setup(r => r.GetByNameAsync("Taken", It.IsAny<CancellationToken>())).ReturnsAsync(conflict);

        var act = () => _sut.UpdateAsync(5, "Taken", ".*", true);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateAsync_SameNameSameId_Succeeds()
    {
        var existing = new LinkType { LinkTypeId = 5, Name = "Same Name", UrlPattern = ".*" };
        _repoMock.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _repoMock.Setup(r => r.GetByNameAsync("Same Name", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await _sut.UpdateAsync(5, "Same Name", @"^updated://", true);

        result.UrlPattern.Should().Be(@"^updated://");
    }

    // ── DeleteAsync ──────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_NonExistingId_ReturnsFalse()
    {
        _repoMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LinkType?)null);

        var result = await _sut.DeleteAsync(999);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_SystemType_ThrowsInvalidOperation()
    {
        var systemType = new LinkType { LinkTypeId = 1, Name = "Direct HTTP", UrlPattern = ".*", IsSystem = true };
        _repoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(systemType);

        var act = () => _sut.DeleteAsync(1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*System link types*");
    }

    [Fact]
    public async Task DeleteAsync_UserType_DelegatesToRepository()
    {
        var userType = new LinkType { LinkTypeId = 5, Name = "Custom", UrlPattern = ".*", IsSystem = false };
        _repoMock.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(userType);
        _repoMock.Setup(r => r.DeleteAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _sut.DeleteAsync(5);

        result.Should().BeTrue();
    }

    // ── ValidateUrlPattern ───────────────────────────────────

    [Theory]
    [InlineData(@"^https?://")]
    [InlineData(@"\.torrent$")]
    [InlineData(@"^magnet:\?")]
    [InlineData(@"(drive\.google|dropbox|mega\.nz)")]
    [InlineData(@"^ftp://[a-z]+")]
    public void ValidateUrlPattern_ValidPatterns_DoesNotThrow(string pattern)
    {
        var act = () => _sut.ValidateUrlPattern(pattern);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(@"[invalid")]
    [InlineData(@"(?P<broken")]
    [InlineData(@"(unclosed")]
    public void ValidateUrlPattern_InvalidRegex_ThrowsArgumentException(string pattern)
    {
        var act = () => _sut.ValidateUrlPattern(pattern);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*not a valid regex*");
    }

    [Theory]
    [InlineData(@"(a+)+")]
    [InlineData(@"(a*)+")]
    [InlineData(@"(a+)*")]
    [InlineData(@"(.+)+")]
    [InlineData(@"(x+){2,}")]
    public void ValidateUrlPattern_NestedQuantifiers_ThrowsArgumentException(string pattern)
    {
        var act = () => _sut.ValidateUrlPattern(pattern);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*nested quantifiers*");
    }

    [Fact]
    public void ValidateUrlPattern_EmptyPattern_ThrowsArgumentException()
    {
        var act = () => _sut.ValidateUrlPattern("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateUrlPattern_PatternTooLong_ThrowsArgumentException()
    {
        var longPattern = new string('a', 1001);

        var act = () => _sut.ValidateUrlPattern(longPattern);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*maximum length*");
    }

    // ── ClassifyUrl ──────────────────────────────────────────

    [Fact]
    public void ClassifyUrl_MatchingHttpUrl_ReturnsLinkTypeId()
    {
        var linkTypes = new List<LinkType>
        {
            new() { LinkTypeId = 1, Name = "Direct HTTP", UrlPattern = @"^https?://", IsActive = true },
            new() { LinkTypeId = 2, Name = "Torrent File", UrlPattern = @"\.torrent$", IsActive = true }
        };

        var result = _sut.ClassifyUrl("https://example.com/file.zip", linkTypes);

        result.Should().Be(1);
    }

    [Fact]
    public void ClassifyUrl_MatchingTorrentUrl_ReturnsCorrectId()
    {
        var linkTypes = new List<LinkType>
        {
            new() { LinkTypeId = 1, Name = "Direct HTTP", UrlPattern = @"^https?://", IsActive = true },
            new() { LinkTypeId = 2, Name = "Torrent File", UrlPattern = @"\.torrent$", IsActive = true }
        };

        var result = _sut.ClassifyUrl("https://example.com/file.torrent", linkTypes);

        // First match wins: Direct HTTP matches first
        result.Should().Be(1);
    }

    [Fact]
    public void ClassifyUrl_NoMatch_ReturnsNull()
    {
        var linkTypes = new List<LinkType>
        {
            new() { LinkTypeId = 2, Name = "Torrent File", UrlPattern = @"\.torrent$", IsActive = true }
        };

        var result = _sut.ClassifyUrl("https://example.com/file.zip", linkTypes);

        result.Should().BeNull();
    }

    [Fact]
    public void ClassifyUrl_InactiveType_IsSkipped()
    {
        var linkTypes = new List<LinkType>
        {
            new() { LinkTypeId = 1, Name = "Direct HTTP", UrlPattern = @"^https?://", IsActive = false },
            new() { LinkTypeId = 2, Name = "Torrent File", UrlPattern = @"\.torrent$", IsActive = true }
        };

        var result = _sut.ClassifyUrl("https://example.com/file.zip", linkTypes);

        result.Should().BeNull();
    }

    [Fact]
    public void ClassifyUrl_EmptyUrl_ReturnsNull()
    {
        var linkTypes = new List<LinkType>
        {
            new() { LinkTypeId = 1, Name = "Direct HTTP", UrlPattern = @"^https?://", IsActive = true }
        };

        var result = _sut.ClassifyUrl("", linkTypes);

        result.Should().BeNull();
    }

    [Fact]
    public void ClassifyUrl_NullUrl_ReturnsNull()
    {
        var linkTypes = new List<LinkType>
        {
            new() { LinkTypeId = 1, Name = "Direct HTTP", UrlPattern = @"^https?://", IsActive = true }
        };

        var result = _sut.ClassifyUrl(null!, linkTypes);

        result.Should().BeNull();
    }

    [Fact]
    public void ClassifyUrl_MagnetUri_Matches()
    {
        var linkTypes = new List<LinkType>
        {
            new() { LinkTypeId = 3, Name = "Magnet URI", UrlPattern = @"^magnet:\?", IsActive = true }
        };

        var result = _sut.ClassifyUrl("magnet:?xt=urn:btih:abc123", linkTypes);

        result.Should().Be(3);
    }

    [Fact]
    public void ClassifyUrl_CloudStorageUrl_Matches()
    {
        var linkTypes = new List<LinkType>
        {
            new() { LinkTypeId = 4, Name = "Cloud Storage URL", UrlPattern = @"(drive\.google|dropbox|mega\.nz)", IsActive = true }
        };

        var result = _sut.ClassifyUrl("https://drive.google.com/file/d/abc", linkTypes);

        result.Should().Be(4);
    }

    [Fact]
    public void ClassifyUrl_TimeoutPattern_SkipsAndReturnsNull()
    {
        // This pattern with nested quantifiers would cause timeout on adversarial input
        // but since we're testing the timeout catch, we use a legitimate slow-ish pattern
        var linkTypes = new List<LinkType>
        {
            new() { LinkTypeId = 99, Name = "Slow", UrlPattern = @"^(a+)+$", IsActive = true }
        };

        // Supply adversarial input that triggers catastrophic backtracking
        var evilInput = new string('a', 30) + "!";
        var result = _sut.ClassifyUrl(evilInput, linkTypes);

        // Should either return null (timeout) or not hang
        result.Should().BeNull();
    }
}
