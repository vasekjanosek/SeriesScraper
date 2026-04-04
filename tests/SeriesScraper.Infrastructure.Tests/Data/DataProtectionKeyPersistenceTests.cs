using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeriesScraper.Infrastructure.Data;

namespace SeriesScraper.Infrastructure.Tests.Data;

public class DataProtectionKeyPersistenceTests
{
    private static AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public void AppDbContext_Implements_IDataProtectionKeyContext()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());

        context.Should().BeAssignableTo<IDataProtectionKeyContext>();
    }

    [Fact]
    public void AppDbContext_Has_DataProtectionKeys_DbSet()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());

        context.DataProtectionKeys.Should().NotBeNull();
    }

    [Fact]
    public void Can_Add_And_Retrieve_DataProtectionKey()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var key = new DataProtectionKey
        {
            FriendlyName = "test-key",
            Xml = "<key id=\"test\"><value>test-xml</value></key>"
        };

        context.DataProtectionKeys.Add(key);
        context.SaveChanges();

        var retrieved = context.DataProtectionKeys.First();
        retrieved.FriendlyName.Should().Be("test-key");
        retrieved.Xml.Should().Contain("test-xml");
        retrieved.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DataProtection_PersistKeysToDbContext_Stores_Keys()
    {
        var dbName = Guid.NewGuid().ToString();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(dbName));
        services.AddDataProtection()
            .SetApplicationName("SeriesScraper.Tests")
            .PersistKeysToDbContext<AppDbContext>();

        var serviceProvider = services.BuildServiceProvider();

        // Force key creation by protecting data
        var protector = serviceProvider.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("test-purpose");
        protector.Protect("test-data");

        // Verify keys were persisted to the database
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.DataProtectionKeys.Should().NotBeEmpty();
    }

    [Fact]
    public void DataProtection_Keys_Survive_Provider_Recreation()
    {
        var dbName = Guid.NewGuid().ToString();
        string encrypted;

        // First provider instance — encrypt
        {
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(dbName));
            services.AddDataProtection()
                .SetApplicationName("SeriesScraper.Tests")
                .PersistKeysToDbContext<AppDbContext>();

            var sp = services.BuildServiceProvider();
            var protector = sp.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("test-purpose");
            encrypted = protector.Protect("secret-value");
            encrypted.Should().NotBeNullOrEmpty();
        }

        // Second provider instance — decrypt using persisted keys
        {
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(dbName));
            services.AddDataProtection()
                .SetApplicationName("SeriesScraper.Tests")
                .PersistKeysToDbContext<AppDbContext>();

            var sp = services.BuildServiceProvider();
            var protector = sp.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("test-purpose");
            var decrypted = protector.Unprotect(encrypted);
            decrypted.Should().Be("secret-value");
        }
    }

    [Fact]
    public void DataProtectionKey_FriendlyName_Can_Be_Null()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var key = new DataProtectionKey
        {
            FriendlyName = null,
            Xml = "<key id=\"null-friendly\"><value>data</value></key>"
        };

        context.DataProtectionKeys.Add(key);
        context.SaveChanges();

        var retrieved = context.DataProtectionKeys.First();
        retrieved.FriendlyName.Should().BeNull();
        retrieved.Xml.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Multiple_DataProtectionKeys_Can_Be_Stored()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        for (int i = 0; i < 5; i++)
        {
            context.DataProtectionKeys.Add(new DataProtectionKey
            {
                FriendlyName = $"key-{i}",
                Xml = $"<key id=\"{i}\"><value>xml-{i}</value></key>"
            });
        }

        context.SaveChanges();

        context.DataProtectionKeys.Count().Should().Be(5);
    }

    [Fact]
    public void DataProtectionKey_Xml_Is_Required()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        // The Xml property is configured as required in the configuration
        var entityType = context.Model.FindEntityType(typeof(DataProtectionKey));
        var xmlProperty = entityType!.FindProperty(nameof(DataProtectionKey.Xml));

        xmlProperty.Should().NotBeNull();
        xmlProperty!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void DataProtectionKey_FriendlyName_MaxLength_Is_500()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var entityType = context.Model.FindEntityType(typeof(DataProtectionKey));
        var prop = entityType!.FindProperty(nameof(DataProtectionKey.FriendlyName));

        prop.Should().NotBeNull();
        prop!.GetMaxLength().Should().Be(500);
    }
}

/// <summary>
/// Integration tests for DataProtection key persistence using a real PostgreSQL database
/// (Testcontainers). These tests verify that keys are written to and read from the
/// actual database, proving persistence across provider/container restarts.
/// </summary>
[Collection("PostgreSQL")]
[Trait("Category", "Integration")]
public class DataProtectionKeyPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly PostgresqlFixture _fixture;

    public DataProtectionKeyPersistenceIntegrationTests(PostgresqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Clean any keys from previous test runs to ensure isolation
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM data_protection_keys");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DataProtection_Keys_Are_Written_To_PostgreSQL_Table()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseNpgsql(_fixture.ConnectionString));
        services.AddDataProtection()
            .SetApplicationName("SeriesScraper.Integration.Tests")
            .PersistKeysToDbContext<AppDbContext>();

        using var sp = services.BuildServiceProvider();

        // Act – protecting data forces key generation and persistence
        var protector = sp.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("storage-test");
        protector.Protect("test-value");

        // Assert – keys exist in the real PostgreSQL table
        await using var verifyContext = _fixture.CreateContext();
        var keyCount = await verifyContext.DataProtectionKeys.CountAsync();
        keyCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Keys_Survive_Provider_Disposal_With_PostgreSQL()
    {
        // Arrange – first provider creates and persists keys to PostgreSQL
        string encrypted;
        {
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(opts =>
                opts.UseNpgsql(_fixture.ConnectionString));
            services.AddDataProtection()
                .SetApplicationName("SeriesScraper.Integration.Tests")
                .PersistKeysToDbContext<AppDbContext>();

            using var sp = services.BuildServiceProvider();
            var protector = sp.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("survive-test");
            encrypted = protector.Protect("secret-payload");
        }
        // All services disposed here — simulates container/process restart

        // Act – new provider reads keys from PostgreSQL (no in-memory state carried over)
        {
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(opts =>
                opts.UseNpgsql(_fixture.ConnectionString));
            services.AddDataProtection()
                .SetApplicationName("SeriesScraper.Integration.Tests")
                .PersistKeysToDbContext<AppDbContext>();

            using var sp = services.BuildServiceProvider();
            var protector = sp.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("survive-test");

            // Assert – data decrypted using keys loaded from PostgreSQL
            var decrypted = protector.Unprotect(encrypted);
            decrypted.Should().Be("secret-payload");
        }
    }

    [Fact]
    public async Task Migration_Creates_DataProtectionKeys_Table_With_Correct_Schema()
    {
        // The fixture's MigrateAsync already applied all migrations when the container started.
        // Verify the data_protection_keys table exists with the correct schema by
        // inserting a row and reading it back.
        await using var context = _fixture.CreateContext();

        var testKey = new DataProtectionKey
        {
            FriendlyName = "schema-test-key",
            Xml = "<key id='schema-test'><creation-date>2026-04-03T00:00:00Z</creation-date></key>"
        };
        context.DataProtectionKeys.Add(testKey);
        await context.SaveChangesAsync();

        var saved = await context.DataProtectionKeys
            .FirstAsync(k => k.FriendlyName == "schema-test-key");

        saved.Id.Should().BeGreaterThan(0);
        saved.FriendlyName.Should().Be("schema-test-key");
        saved.Xml.Should().NotBeNullOrEmpty();
    }
}
