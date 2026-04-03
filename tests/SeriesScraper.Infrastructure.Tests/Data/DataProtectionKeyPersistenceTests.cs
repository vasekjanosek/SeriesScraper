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
