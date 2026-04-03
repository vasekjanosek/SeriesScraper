namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Service contract for Forum CRUD operations.
/// Handles URL validation, credential encryption, and denormalized history on delete.
/// </summary>
public interface IForumCrudService
{
    Task<IReadOnlyList<ForumDto>> GetAllForumsAsync(CancellationToken ct = default);
    Task<ForumDto?> GetForumByIdAsync(int forumId, CancellationToken ct = default);
    Task<ForumDto> CreateForumAsync(CreateForumDto dto, CancellationToken ct = default);
    Task<ForumDto> UpdateForumAsync(int forumId, UpdateForumDto dto, CancellationToken ct = default);
    Task DeleteForumAsync(int forumId, CancellationToken ct = default);
}

/// <summary>
/// Forum data returned to the UI. Never contains raw credentials.
/// </summary>
public record ForumDto
{
    public int ForumId { get; init; }
    public required string Name { get; init; }
    public required string BaseUrl { get; init; }
    public required string Username { get; init; }
    public required string CredentialKey { get; init; }
    public int CrawlDepth { get; init; }
    public int PolitenessDelayMs { get; init; }
    public bool IsActive { get; init; }
    public bool HasPassword { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// DTO for creating a new forum.
/// </summary>
public record CreateForumDto
{
    public required string Name { get; init; }
    public required string BaseUrl { get; init; }
    public required string Username { get; init; }
    public required string CredentialKey { get; init; }
    public string? Password { get; init; }
    public int CrawlDepth { get; init; } = 1;
    public int PolitenessDelayMs { get; init; } = 500;
}

/// <summary>
/// DTO for updating an existing forum. Password is only updated if provided.
/// </summary>
public record UpdateForumDto
{
    public required string Name { get; init; }
    public required string BaseUrl { get; init; }
    public required string Username { get; init; }
    public required string CredentialKey { get; init; }
    public string? Password { get; init; }
    public int CrawlDepth { get; init; }
    public int PolitenessDelayMs { get; init; }
    public bool IsActive { get; init; }
}
