using Microsoft.AspNetCore.DataProtection;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Infrastructure.Services;

/// <summary>
/// Encrypts and decrypts forum credentials using ASP.NET Core DataProtection API.
/// Purpose string scopes protection to forum credentials only.
/// </summary>
public class CredentialProtector : ICredentialProtector
{
    private const string Purpose = "SeriesScraper.ForumCredentials";
    private readonly IDataProtector _protector;

    public CredentialProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Encrypt(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);
        return _protector.Protect(plaintext);
    }

    public string Decrypt(string encryptedValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(encryptedValue);
        return _protector.Unprotect(encryptedValue);
    }
}
