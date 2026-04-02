using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.InMemory;
using SeriesScraper.Web.Logging;

namespace SeriesScraper.Web.Tests.Logging;

public class SerilogConfigurationTests
{
    [Fact]
    public void Serilog_ConfiguredFromAppsettings_ReadsMinimumLevel()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.test.json")
            .Build();

        var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.InMemory()
            .CreateLogger();

        // Act
        logger.Information("Test info message");
        logger.Debug("Test debug message");

        // Assert — default minimum level is Information so Debug should be filtered
        InMemorySink.Instance.LogEvents.Should().ContainSingle()
            .Which.MessageTemplate.Text.Should().Be("Test info message");

        logger.Dispose();
        InMemorySink.Instance.Dispose();
    }

    [Fact]
    public void Serilog_ConfiguredFromAppsettings_AspNetCoreOverrideIsWarning()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.test.json")
            .Build();

        var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.InMemory()
            .CreateLogger();

        // Act — simulate ASP.NET Core log at Information (should be filtered)
        var aspNetLogger = logger.ForContext(Serilog.Core.Constants.SourceContextPropertyName, "Microsoft.AspNetCore.Routing");
        aspNetLogger.Information("Request routed");
        aspNetLogger.Warning("Request warning");

        // Assert
        InMemorySink.Instance.LogEvents.Should().ContainSingle()
            .Which.Level.Should().Be(LogEventLevel.Warning);

        logger.Dispose();
        InMemorySink.Instance.Dispose();
    }

    [Fact]
    public void Serilog_WithEnvironmentVariableOverride_ChangesMinimumLevel()
    {
        // Arrange — override minimum level to Debug via config
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Serilog:MinimumLevel:Default"] = "Debug"
            })
            .Build();

        var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.InMemory()
            .CreateLogger();

        // Act
        logger.Debug("Debug message with override");

        // Assert
        InMemorySink.Instance.LogEvents.Should().ContainSingle()
            .Which.Level.Should().Be(LogEventLevel.Debug);

        logger.Dispose();
        InMemorySink.Instance.Dispose();
    }

    [Fact]
    public void Serilog_WithCredentialPolicy_RedactsSensitiveFields()
    {
        // Arrange
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Destructure.With<CredentialDestructuringPolicy>()
            .WriteTo.InMemory()
            .CreateLogger();

        var credentials = new { Username = "admin", Password = "secret123", ForumUrl = "https://example.com" };

        // Act
        logger.Information("Connecting with {@Credentials}", credentials);

        // Assert
        var logEvent = InMemorySink.Instance.LogEvents.Should().ContainSingle().Subject;
        var rendered = logEvent.Properties["Credentials"].ToString();
        rendered.Should().NotContain("admin");
        rendered.Should().NotContain("secret123");
        rendered.Should().Contain("[REDACTED]");
        rendered.Should().Contain("https://example.com");

        logger.Dispose();
        InMemorySink.Instance.Dispose();
    }

    [Fact]
    public void Serilog_StructuredLogging_CapturesStructuredProperties()
    {
        // Arrange
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.InMemory()
            .CreateLogger();

        // Act
        logger.Information("Run started: {RunId} for {ForumId} with {ItemCount} items",
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), "forum-1", 42);

        // Assert
        var logEvent = InMemorySink.Instance.LogEvents.Should().ContainSingle().Subject;
        logEvent.Properties.Should().ContainKey("RunId");
        logEvent.Properties.Should().ContainKey("ForumId");
        logEvent.Properties.Should().ContainKey("ItemCount");
        logEvent.Properties["ItemCount"].ToString().Should().Be("42");

        logger.Dispose();
        InMemorySink.Instance.Dispose();
    }

    [Fact]
    public void Serilog_ConsoleSink_IsConfigured()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.test.json")
            .Build();

        // Act — should not throw; verifies the Console sink config is valid
        var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        // Assert — logger was created successfully with config
        logger.Should().NotBeNull();

        logger.Dispose();
    }

    [Fact]
    public void Serilog_FileSink_ConfiguredWithRollingInterval()
    {
        // Arrange — verify file sink configuration is parseable
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.test.json")
            .Build();

        var fileSinkConfig = configuration.GetSection("Serilog:WriteTo:1:Args");

        // Assert
        fileSinkConfig["rollingInterval"].Should().Be("Day");
        fileSinkConfig["retainedFileCountLimit"].Should().Be("30");
        fileSinkConfig["path"].Should().NotBeNullOrWhiteSpace();
    }
}
