using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using SeriesScraper.Infrastructure.Services;

namespace SeriesScraper.Infrastructure.Tests.Services;

public class CredentialProtectorTests
{
    private readonly CredentialProtector _sut;

    public CredentialProtectorTests()
    {
        var services = new ServiceCollection();
        services.AddDataProtection()
            .SetApplicationName("SeriesScraper.Tests");
        var provider = services.BuildServiceProvider();
        var dpProvider = provider.GetRequiredService<IDataProtectionProvider>();
        _sut = new CredentialProtector(dpProvider);
    }

    [Fact]
    public void Encrypt_ReturnsNonEmptyString()
    {
        var result = _sut.Encrypt("mysecret");

        result.Should().NotBeNullOrEmpty();
        result.Should().NotBe("mysecret");
    }

    [Fact]
    public void Decrypt_RoundTrip_ReturnsOriginal()
    {
        var original = "my-super-secret-password!@#$";
        var encrypted = _sut.Encrypt(original);
        var decrypted = _sut.Decrypt(encrypted);

        decrypted.Should().Be(original);
    }

    [Fact]
    public void Encrypt_DifferentInputs_ProduceDifferentCiphertext()
    {
        var enc1 = _sut.Encrypt("password1");
        var enc2 = _sut.Encrypt("password2");

        enc1.Should().NotBe(enc2);
    }

    [Fact]
    public void Encrypt_EmptyString_ThrowsArgumentException()
    {
        var act = () => _sut.Encrypt("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Encrypt_Null_ThrowsArgumentException()
    {
        var act = () => _sut.Encrypt(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Decrypt_EmptyString_ThrowsArgumentException()
    {
        var act = () => _sut.Decrypt("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Decrypt_InvalidCiphertext_ThrowsException()
    {
        var act = () => _sut.Decrypt("not-a-valid-encrypted-value");

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Encrypt_SpecialCharacters_RoundTripsCorrectly()
    {
        var original = "p@$$w0rd with spaces & unicode: 日本語";
        var encrypted = _sut.Encrypt(original);
        var decrypted = _sut.Decrypt(encrypted);

        decrypted.Should().Be(original);
    }

    [Fact]
    public void Encrypt_LongPassword_RoundTripsCorrectly()
    {
        var original = new string('A', 1000);
        var encrypted = _sut.Encrypt(original);
        var decrypted = _sut.Decrypt(encrypted);

        decrypted.Should().Be(original);
    }
}
