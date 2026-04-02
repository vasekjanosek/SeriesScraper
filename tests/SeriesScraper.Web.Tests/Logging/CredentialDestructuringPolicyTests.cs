using FluentAssertions;
using Serilog.Events;
using Serilog.Core;
using SeriesScraper.Web.Logging;

namespace SeriesScraper.Web.Tests.Logging;

public class CredentialDestructuringPolicyTests
{
    private readonly CredentialDestructuringPolicy _policy = new();
    private readonly ILogEventPropertyValueFactory _factory = new TestPropertyValueFactory();

    [Fact]
    public void TryDestructure_NullValue_ReturnsFalse()
    {
        var result = _policy.TryDestructure(null!, _factory, out var value);

        result.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void TryDestructure_PrimitiveValue_ReturnsFalse()
    {
        var result = _policy.TryDestructure(42, _factory, out var value);

        result.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void TryDestructure_StringValue_ReturnsFalse()
    {
        var result = _policy.TryDestructure("hello", _factory, out var value);

        result.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void TryDestructure_ObjectWithPassword_RedactsPassword()
    {
        var obj = new { Password = "super-secret", Name = "test" };

        var result = _policy.TryDestructure(obj, _factory, out var value);

        result.Should().BeTrue();
        var structure = value.Should().BeOfType<StructureValue>().Subject;
        var passwordProp = structure.Properties.Single(p => p.Name == "Password");
        passwordProp.Value.ToString().Should().Contain("[REDACTED]");
    }

    [Fact]
    public void TryDestructure_ObjectWithCredentialKey_RedactsCredentialKey()
    {
        var obj = new { CredentialKey = "key-123", ForumId = 5 };

        var result = _policy.TryDestructure(obj, _factory, out var value);

        result.Should().BeTrue();
        var structure = value.Should().BeOfType<StructureValue>().Subject;
        var prop = structure.Properties.Single(p => p.Name == "CredentialKey");
        prop.Value.ToString().Should().Contain("[REDACTED]");
    }

    [Fact]
    public void TryDestructure_ObjectWithAccessToken_RedactsAccessToken()
    {
        var obj = new { AccessToken = "bearer-xyz", Endpoint = "https://example.com" };

        var result = _policy.TryDestructure(obj, _factory, out var value);

        result.Should().BeTrue();
        var structure = value.Should().BeOfType<StructureValue>().Subject;
        var prop = structure.Properties.Single(p => p.Name == "AccessToken");
        prop.Value.ToString().Should().Contain("[REDACTED]");
    }

    [Fact]
    public void TryDestructure_ObjectWithUsername_RedactsUsername()
    {
        var obj = new { Username = "admin", Email = "test@example.com" };

        var result = _policy.TryDestructure(obj, _factory, out var value);

        result.Should().BeTrue();
        var structure = value.Should().BeOfType<StructureValue>().Subject;
        var prop = structure.Properties.Single(p => p.Name == "Username");
        prop.Value.ToString().Should().Contain("[REDACTED]");
    }

    [Fact]
    public void TryDestructure_ObjectWithNonSensitiveProperties_PreservesValues()
    {
        var obj = new { Name = "test-forum", Url = "https://forum.example.com" };

        var result = _policy.TryDestructure(obj, _factory, out var value);

        result.Should().BeTrue();
        var structure = value.Should().BeOfType<StructureValue>().Subject;
        var nameProp = structure.Properties.Single(p => p.Name == "Name");
        nameProp.Value.ToString().Should().NotContain("[REDACTED]");
    }

    [Fact]
    public void TryDestructure_SensitivePropertyNames_AreCaseInsensitive()
    {
        var obj = new ObjectWithMixedCaseCredentials { password = "secret", USERNAME = "admin", Value = "ok" };

        var result = _policy.TryDestructure(obj, _factory, out var value);

        result.Should().BeTrue();
        var structure = value.Should().BeOfType<StructureValue>().Subject;

        var passwordProp = structure.Properties.Single(p => p.Name == "password");
        passwordProp.Value.ToString().Should().Contain("[REDACTED]");

        var usernameProp = structure.Properties.Single(p => p.Name == "USERNAME");
        usernameProp.Value.ToString().Should().Contain("[REDACTED]");
    }

    [Fact]
    public void TryDestructure_MultipleSensitiveProperties_RedactsAll()
    {
        var obj = new { Password = "pw", CredentialKey = "ck", AccessToken = "at", Username = "u", SafeField = "ok" };

        var result = _policy.TryDestructure(obj, _factory, out var value);

        result.Should().BeTrue();
        var structure = value.Should().BeOfType<StructureValue>().Subject;

        structure.Properties.Where(p => p.Name is "Password" or "CredentialKey" or "AccessToken" or "Username")
            .Should().AllSatisfy(p => p.Value.ToString().Should().Contain("[REDACTED]"));

        structure.Properties.Single(p => p.Name == "SafeField")
            .Value.ToString().Should().NotContain("[REDACTED]");
    }

    [Fact]
    public void TryDestructure_IncludesTypeName()
    {
        var obj = new SampleObject { Id = 1 };

        var result = _policy.TryDestructure(obj, _factory, out var value);

        result.Should().BeTrue();
        var structure = value.Should().BeOfType<StructureValue>().Subject;
        structure.TypeTag.Should().Be(nameof(SampleObject));
    }

    // Helper types for testing
    private sealed class ObjectWithMixedCaseCredentials
    {
        public string password { get; set; } = "";
        public string USERNAME { get; set; } = "";
        public string Value { get; set; } = "";
    }

    private sealed class SampleObject
    {
        public int Id { get; set; }
    }

    /// <summary>
    /// Minimal ILogEventPropertyValueFactory for testing.
    /// </summary>
    private sealed class TestPropertyValueFactory : ILogEventPropertyValueFactory
    {
        public LogEventPropertyValue CreatePropertyValue(object? value, bool destructureObjects = false)
        {
            return new ScalarValue(value);
        }
    }
}
