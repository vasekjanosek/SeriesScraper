using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Application.Services;

/// <summary>
/// Implements Forum CRUD operations with URL validation, credential encryption,
/// and denormalized history preservation on delete.
/// </summary>
public class ForumCrudService : IForumCrudService
{
    private readonly IForumRepository _forumRepository;
    private readonly IUrlValidator _urlValidator;
    private readonly ICredentialProtector _credentialProtector;
    private readonly ILogger<ForumCrudService> _logger;

    public ForumCrudService(
        IForumRepository forumRepository,
        IUrlValidator urlValidator,
        ICredentialProtector credentialProtector,
        ILogger<ForumCrudService> logger)
    {
        _forumRepository = forumRepository;
        _urlValidator = urlValidator;
        _credentialProtector = credentialProtector;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ForumDto>> GetAllForumsAsync(CancellationToken ct = default)
    {
        var forums = await _forumRepository.GetAllAsync(ct);
        return forums.Select(MapToDto).ToList().AsReadOnly();
    }

    public async Task<ForumDto?> GetForumByIdAsync(int forumId, CancellationToken ct = default)
    {
        var forum = await _forumRepository.GetByIdAsync(forumId, ct);
        return forum is null ? null : MapToDto(forum);
    }

    public async Task<ForumDto> CreateForumAsync(CreateForumDto dto, CancellationToken ct = default)
    {
        ValidateUrl(dto.BaseUrl);
        _ = new CredentialKey(dto.CredentialKey);

        var now = DateTime.UtcNow;
        var forum = new Forum
        {
            Name = dto.Name,
            BaseUrl = dto.BaseUrl,
            Username = dto.Username,
            CredentialKey = dto.CredentialKey,
            CrawlDepth = dto.CrawlDepth,
            PolitenessDelayMs = dto.PolitenessDelayMs,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (!string.IsNullOrEmpty(dto.Password))
        {
            forum.EncryptedPassword = _credentialProtector.Encrypt(dto.Password);
        }

        await _forumRepository.AddAsync(forum, ct);
        _logger.LogInformation("Forum '{ForumName}' created with ID {ForumId}", forum.Name, forum.ForumId);
        return MapToDto(forum);
    }

    public async Task<ForumDto> UpdateForumAsync(int forumId, UpdateForumDto dto, CancellationToken ct = default)
    {
        var forum = await _forumRepository.GetByIdAsync(forumId, ct)
            ?? throw new InvalidOperationException($"Forum with ID {forumId} not found.");

        ValidateUrl(dto.BaseUrl);
        _ = new CredentialKey(dto.CredentialKey);

        forum.Name = dto.Name;
        forum.BaseUrl = dto.BaseUrl;
        forum.Username = dto.Username;
        forum.CredentialKey = dto.CredentialKey;
        forum.CrawlDepth = dto.CrawlDepth;
        forum.PolitenessDelayMs = dto.PolitenessDelayMs;
        forum.IsActive = dto.IsActive;
        forum.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(dto.Password))
        {
            forum.EncryptedPassword = _credentialProtector.Encrypt(dto.Password);
        }

        await _forumRepository.UpdateAsync(forum, ct);
        _logger.LogInformation("Forum '{ForumName}' (ID {ForumId}) updated", forum.Name, forum.ForumId);
        return MapToDto(forum);
    }

    public async Task DeleteForumAsync(int forumId, CancellationToken ct = default)
    {
        var forum = await _forumRepository.GetByIdAsync(forumId, ct)
            ?? throw new InvalidOperationException($"Forum with ID {forumId} not found.");

        // Denormalize forum name on history records before deletion
        await _forumRepository.DenormalizeForumNameOnRunsAsync(forumId, forum.Name, ct);

        await _forumRepository.DeleteAsync(forum, ct);
        _logger.LogInformation("Forum '{ForumName}' (ID {ForumId}) deleted", forum.Name, forumId);
    }

    private void ValidateUrl(string url)
    {
        if (!_urlValidator.IsUrlSafe(url, out var reason))
        {
            throw new ArgumentException($"Forum URL is not valid: {reason}");
        }
    }

    private static ForumDto MapToDto(Forum forum)
    {
        return new ForumDto
        {
            ForumId = forum.ForumId,
            Name = forum.Name,
            BaseUrl = forum.BaseUrl,
            Username = forum.Username,
            CredentialKey = forum.CredentialKey,
            CrawlDepth = forum.CrawlDepth,
            PolitenessDelayMs = forum.PolitenessDelayMs,
            IsActive = forum.IsActive,
            HasPassword = !string.IsNullOrEmpty(forum.EncryptedPassword),
            CreatedAt = forum.CreatedAt,
            UpdatedAt = forum.UpdatedAt
        };
    }
}
