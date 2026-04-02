using System.Text.RegularExpressions;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Services;

public class LinkTypeService : ILinkTypeService
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);
    private const int MaxPatternLength = 1000;

    // Detect patterns prone to catastrophic backtracking:
    // nested quantifiers like (a+)+, (a*)+, (a+)*, (.+)+ etc.
    private static readonly Regex NestedQuantifierPattern = new(
        @"\([^)]*[+*][^)]*\)[+*?]|\([^)]*[+*][^)]*\)\{",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private readonly ILinkTypeRepository _repository;

    public LinkTypeService(ILinkTypeRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<LinkType>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public Task<IReadOnlyList<LinkType>> GetActiveAsync(CancellationToken cancellationToken = default)
        => _repository.GetActiveAsync(cancellationToken);

    public Task<LinkType?> GetByIdAsync(int linkTypeId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(linkTypeId, cancellationToken);

    public async Task<LinkType> CreateAsync(string name, string urlPattern, string? iconClass = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(urlPattern);

        ValidateUrlPattern(urlPattern);

        if (await _repository.ExistsByNameAsync(name, cancellationToken))
            throw new InvalidOperationException($"A link type with the name '{name}' already exists.");

        var linkType = new LinkType
        {
            Name = name.Trim(),
            UrlPattern = urlPattern.Trim(),
            IconClass = iconClass?.Trim(),
            IsSystem = false,
            IsActive = true
        };

        return await _repository.AddAsync(linkType, cancellationToken);
    }

    public async Task<LinkType> UpdateAsync(int linkTypeId, string name, string urlPattern, bool isActive, string? iconClass = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(urlPattern);

        ValidateUrlPattern(urlPattern);

        var existing = await _repository.GetByIdAsync(linkTypeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Link type with ID {linkTypeId} not found.");

        var byName = await _repository.GetByNameAsync(name, cancellationToken);
        if (byName is not null && byName.LinkTypeId != linkTypeId)
            throw new InvalidOperationException($"A link type with the name '{name}' already exists.");

        existing.Name = name.Trim();
        existing.UrlPattern = urlPattern.Trim();
        existing.IsActive = isActive;
        existing.IconClass = iconClass?.Trim();

        await _repository.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(int linkTypeId, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(linkTypeId, cancellationToken);
        if (existing is null)
            return false;

        if (existing.IsSystem)
            throw new InvalidOperationException("System link types cannot be deleted.");

        return await _repository.DeleteAsync(linkTypeId, cancellationToken);
    }

    public void ValidateUrlPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("URL pattern cannot be empty.", nameof(pattern));

        if (pattern.Length > MaxPatternLength)
            throw new ArgumentException($"URL pattern exceeds maximum length of {MaxPatternLength} characters.", nameof(pattern));

        // ReDoS prevention: reject patterns with nested quantifiers
        if (NestedQuantifierPattern.IsMatch(pattern))
            throw new ArgumentException("URL pattern contains nested quantifiers which may cause catastrophic backtracking (ReDoS).", nameof(pattern));

        // Validate regex compilation with timeout
        try
        {
            _ = new Regex(pattern, RegexOptions.None, RegexTimeout);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"URL pattern is not a valid regex: {ex.Message}", nameof(pattern), ex);
        }
    }

    public int? ClassifyUrl(string url, IReadOnlyList<LinkType> linkTypes)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        foreach (var linkType in linkTypes)
        {
            if (!linkType.IsActive)
                continue;

            try
            {
                var regex = new Regex(linkType.UrlPattern, RegexOptions.IgnoreCase, RegexTimeout);
                if (regex.IsMatch(url))
                    return linkType.LinkTypeId;
            }
            catch (RegexMatchTimeoutException)
            {
                // ReDoS prevention: pattern timed out, skip this type
                continue;
            }
        }

        return null;
    }
}
