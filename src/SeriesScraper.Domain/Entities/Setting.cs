namespace SeriesScraper.Domain.Entities;

/// <summary>
/// Key-value store for ALL application configuration.
/// ALL runtime config must be stored here, not in appsettings.json.
/// </summary>
public class Setting
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public string? Description { get; set; }
    public DateTime LastModifiedAt { get; set; }
}
