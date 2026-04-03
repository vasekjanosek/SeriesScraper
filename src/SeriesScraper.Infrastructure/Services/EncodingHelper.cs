using System.Text;

namespace SeriesScraper.Infrastructure.Services;

/// <summary>
/// Provides encoding-aware reading of HTTP response content.
/// Supports legacy encodings such as Windows-1250 (Czech) that require
/// CodePagesEncodingProvider to be registered.
/// </summary>
public static class EncodingHelper
{
    /// <summary>
    /// Reads an HttpResponseMessage body as a string using the specified encoding name.
    /// Falls back to UTF-8 if the encoding name is null, empty, or unrecognized.
    /// </summary>
    /// <param name="response">The HTTP response to read.</param>
    /// <param name="encodingName">
    /// The IANA encoding name (e.g. "utf-8", "windows-1250").
    /// If null or empty, defaults to UTF-8.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response body decoded with the specified encoding.</returns>
    public static async Task<string> ReadResponseAsStringAsync(
        HttpResponseMessage response,
        string? encodingName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        var encoding = ResolveEncoding(encodingName);

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, encoding);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    /// <summary>
    /// Resolves an encoding by IANA name. Returns UTF-8 if the name is null, empty,
    /// or not recognized.
    /// </summary>
    public static Encoding ResolveEncoding(string? encodingName)
    {
        if (string.IsNullOrWhiteSpace(encodingName))
            return Encoding.UTF8;

        try
        {
            return Encoding.GetEncoding(encodingName);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    /// <summary>
    /// Registers the CodePagesEncodingProvider so that legacy encodings
    /// (e.g. Windows-1250) are available on .NET Core / .NET 5+.
    /// Safe to call multiple times.
    /// </summary>
    public static void RegisterCodePages()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}
