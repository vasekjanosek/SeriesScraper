using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Tests.Data;

public class LinkTypeRepositoryTests
{
    private static (AppDbContext Context, LinkTypeRepository Repository) CreateSut()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return (context, new LinkTypeRepository(context));
    }

    // ── GetAllAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsSeedData()
    {
        var (context, sut) = CreateSut();
        using var _ = context;

        var result = await sut.GetAllAsync();

        result.Should().HaveCountGreaterOrEqualTo(4);
        result.Should().Contain(lt => lt.Name == "Direct HTTP");
        result.Should().Contain(lt => lt.Name == "Torrent File");
        result.Should().Contain(lt => lt.Name == "Magnet URI");
        result.Should().Contain(lt => lt.Name == "Cloud Storage URL");
    }

    [Fact]
    public async Task GetAllAsync_IncludesInactiveTypes()
    {
        var (context, sut) = CreateSut();
        using var _ = context;

        context.LinkTypes.Add(new LinkType { Name = "Inactive", UrlPattern = ".*", IsActive = false });
        await context.SaveChangesAsync();

        var result = await sut.GetAllAsync();

        result.Should().Contain(lt => lt.Name == "Inactive");
    }

    // ── GetActiveAsync ───────────────────────────────────────

    [Fact]
    public async Task GetActiveAsync_ExcludesInactiveTypes()
    {
        var (context, sut) = CreateSut();
        using var _ = context;

        context.LinkTypes.Add(new LinkType { Name = "Inactive", UrlPattern = ".*", IsActive = false });
        await context.SaveChangesAsync();

        var result = await sut.GetActiveAsync();

        result.Should().NotContain(lt => lt.Name == "Inactive");
        result.Should().OnlyContain(lt => lt.IsActive);
    }

    // ── GetByIdAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsLinkType()
    {
        var (context, sut) = CreateSut();
        using var _ = context;

        var result = await sut.GetByIdAsync(1);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Direct HTTP");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        var (context, sut) = CreateSut();
        using var _ = context;

        var result = await sut.GetByIdAsync(999);

        result.Should().BeNull();
    }

    // ── GetByNameAsync ───────────────────────────────────────

    [Fact]
    public async Task GetByNameAsync_ExistingName_ReturnsLinkType()
    {
        var (context, sut) = CreateSut();
        using var _ = context;

        var result = await sut.GetByNameAsync("Magnet URI");

        result.Should().NotBeNull();
        result!.LinkTypeId.Should().Be(3);
    }

    [Fact]
    public async Task GetByNameAsync_NonExistingName_ReturnsNull()
    {
        var (context, sut) = CreateSut();
        using var _ = context;

        var result = await sut.GetByNameAsync("Nonexistent");

        result.Should().BeNull();
    }

    // ── AddAsync ─────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_ValidLinkType_PersistsAndReturnsWithId()
    {
        var (context, sut) = CreateSut();
        using var _ = context;

        var linkType = new LinkType
        {
            Name = "FTP Link",
            UrlPattern = @"^ftp://",
            IsSystem = false,
            IsActive = true
        };

        var result = await sut.AddAsync(linkType);

        result.LinkTypeId.Should().BeGreaterThan(0);

        var fromDb = await context.LinkTypes.FindAsync(result.LinkTypeId);
        fromDb.Should().NotBeNull();
        fromDb!.Name.Should().Be("FTP Link");
    }

    // ── UpdateAsync ──────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ModifiesExistingEntity()
    {
        var (context, sut) = CreateSut();
        using var _ = context;

        var existing = await sut.GetByIdAsync(1);
        existing!.IconClass = "fa-globe";

        await sut.UpdateAsync(existing);

        var updated = await context.LinkTypes.FindAsync(1);
        updated!.IconClass.Should().Be("fa-globe");
    }

    // ── DeleteAsync ──────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_UserType_RemovesAndReturnsTrue()
    {
        var (context, sut) = CreateSut();
        using var _ = context;

        var userType = new LinkType { Name = "Temp", UrlPattern = ".*", IsSystem = false, IsActive = true };
        context.LinkTypes.Add(userType);
        await context.SaveChangesAsync();

        var result = await sut.DeleteAsync(userType.LinkTypeId);

        result.Should().BeTrue();
        (await context.LinkTypes.FindAsync(userType.LinkTypeId)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_SystemType_ReturnsFalse()
    {
        var (context, sut) = CreateSut();
        using var _ = context;

        var result = await sut.DeleteAsync(1); // Direct HTTP is system

        result.Should().BeFalse();
        (await context.LinkTypes.FindAsync(1)).Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistingId_ReturnsFalse()
    {
        var (context, sut) = CreateSut();
        using var _ = context;

        var result = await sut.DeleteAsync(999);

        result.Should().BeFalse();
    }

    // ── ExistsByNameAsync ────────────────────────────────────

    [Fact]
    public async Task ExistsByNameAsync_ExistingName_ReturnsTrue()
    {
        var (context, sut) = CreateSut();
        using var _ = context;

        var result = await sut.ExistsByNameAsync("Direct HTTP");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByNameAsync_NonExistingName_ReturnsFalse()
    {
        var (context, sut) = CreateSut();
        using var _ = context;

        var result = await sut.ExistsByNameAsync("Nonexistent");

        result.Should().BeFalse();
    }
}
