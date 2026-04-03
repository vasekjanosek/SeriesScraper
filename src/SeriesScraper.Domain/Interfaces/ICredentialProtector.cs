namespace SeriesScraper.Domain.Interfaces;

/// <summary>
/// Encrypts and decrypts forum credentials using ASP.NET Core DataProtection API.
/// The encryption key is sourced from an environment variable.
/// </summary>
public interface ICredentialProtector
{
    /// <summary>
    /// Encrypts a plaintext password for secure storage.
    /// </summary>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts an encrypted password back to plaintext.
    /// </summary>
    string Decrypt(string encryptedValue);
}
