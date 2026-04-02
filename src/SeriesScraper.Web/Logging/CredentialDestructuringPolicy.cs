using Serilog.Core;
using Serilog.Events;

namespace SeriesScraper.Web.Logging;

/// <summary>
/// Serilog destructuring policy that redacts credential-related properties
/// from structured log output. Prevents passwords, tokens and usernames
/// from appearing in any log sink.
/// </summary>
public sealed class CredentialDestructuringPolicy : IDestructuringPolicy
{
    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "CredentialKey",
        "AccessToken",
        "Username"
    };

    private const string RedactedValue = "[REDACTED]";

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        result = null;

        if (value is null || value.GetType().IsPrimitive || value is string)
            return false;

        var type = value.GetType();
        var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        if (properties.Length == 0)
            return false;

        var logProperties = new List<LogEventProperty>();

        foreach (var prop in properties)
        {
            if (!prop.CanRead)
                continue;

            LogEventPropertyValue propValue;

            if (SensitivePropertyNames.Contains(prop.Name))
            {
                propValue = new ScalarValue(RedactedValue);
            }
            else
            {
                try
                {
                    var rawValue = prop.GetValue(value);
                    propValue = propertyValueFactory.CreatePropertyValue(rawValue, destructureObjects: true);
                }
                catch
                {
                    propValue = new ScalarValue("(error reading property)");
                }
            }

            logProperties.Add(new LogEventProperty(prop.Name, propValue));
        }

        result = new StructureValue(logProperties, type.Name);
        return true;
    }
}
