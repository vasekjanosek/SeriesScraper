using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Tests.Services;

public class SettingsServiceTests
{
    private readonly ISettingRepository _settingRepository;
    private readonly IDataSourceImportRunRepository _importRunRepository;
    private readonly ILogger<SettingsService> _logger;
    private readonly SettingsService _sut;

    public SettingsServiceTests()
    {
        _settingRepository = Substitute.For<ISettingRepository>();
        _importRunRepository = Substitute.For<IDataSourceImportRunRepository>();
        _logger = Substitute.For<ILogger<SettingsService>>();

        _sut = new SettingsService(
            _settingRepository,
            _importRunRepository,
            _logger);
    }

    // ─── GetAllSettingsAsync ──────────────────────────────────────

    [Fact]
    public async Task GetAllSettingsAsync_ReturnsList_WhenSettingsExist()
    {
        var settings = new List<Setting>
        {
            new() { Key = "Key1", Value = "Val1", LastModifiedAt = DateTime.UtcNow },
            new() { Key = "Key2", Value = "Val2", LastModifiedAt = DateTime.UtcNow }
        };
        _settingRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var result = await _sut.GetAllSettingsAsync();

        result.Should().HaveCount(2);
        result[0].Key.Should().Be("Key1");
    }

    [Fact]
    public async Task GetAllSettingsAsync_ReturnsEmpty_WhenNoSettings()
    {
        _settingRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Setting>());

        var result = await _sut.GetAllSettingsAsync();

        result.Should().BeEmpty();
    }

    // ─── UpdateSettingAsync ───────────────────────────────────────

    [Fact]
    public async Task UpdateSettingAsync_CallsRepository_WhenValidKeyAndValue()
    {
        await _sut.UpdateSettingAsync("MyKey", "MyValue");

        await _settingRepository.Received(1).UpdateAsync("MyKey", "MyValue", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateSettingAsync_ThrowsArgumentException_WhenKeyIsEmpty()
    {
        var act = () => _sut.UpdateSettingAsync("", "value");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("key");
    }

    [Fact]
    public async Task UpdateSettingAsync_ThrowsArgumentException_WhenKeyIsWhitespace()
    {
        var act = () => _sut.UpdateSettingAsync("   ", "value");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("key");
    }

    [Fact]
    public async Task UpdateSettingAsync_ThrowsArgumentNullException_WhenValueIsNull()
    {
        var act = () => _sut.UpdateSettingAsync("key", null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("value");
    }

    [Fact]
    public async Task UpdateSettingAsync_AllowsEmptyStringValue()
    {
        await _sut.UpdateSettingAsync("key", "");

        await _settingRepository.Received(1).UpdateAsync("key", "", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateSettingAsync_PropagatesRepositoryException()
    {
        _settingRepository.UpdateAsync("MissingKey", "v", Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Setting 'MissingKey' not found."));

        var act = () => _sut.UpdateSettingAsync("MissingKey", "v");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*MissingKey*");
    }

    // ─── GetImdbImportStatusAsync ─────────────────────────────────

    [Fact]
    public async Task GetImdbImportStatusAsync_ReturnsStatus_WhenLastRunCompleted()
    {
        var finishedAt = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        var lastRun = new DataSourceImportRun
        {
            ImportRunId = 1,
            SourceId = 1,
            StartedAt = finishedAt.AddMinutes(-30),
            FinishedAt = finishedAt,
            Status = "Complete",
            RowsImported = 50000
        };
        _importRunRepository.GetLastImportRunAsync(1, Arg.Any<CancellationToken>()).Returns(lastRun);
        _settingRepository.GetValueAsync("ImdbRefreshIntervalHours", Arg.Any<CancellationToken>()).Returns("24");

        var result = await _sut.GetImdbImportStatusAsync();

        result.LastImportDate.Should().Be(finishedAt);
        result.RowsImported.Should().Be(50000);
        result.Status.Should().Be("Complete");
        result.NextScheduledRun.Should().Be(finishedAt.AddHours(24));
    }

    [Fact]
    public async Task GetImdbImportStatusAsync_ReturnsStartedAt_WhenRunStillRunning()
    {
        var startedAt = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        var lastRun = new DataSourceImportRun
        {
            ImportRunId = 1,
            SourceId = 1,
            StartedAt = startedAt,
            FinishedAt = null,
            Status = "Running",
            RowsImported = 1000
        };
        _importRunRepository.GetLastImportRunAsync(1, Arg.Any<CancellationToken>()).Returns(lastRun);
        _settingRepository.GetValueAsync("ImdbRefreshIntervalHours", Arg.Any<CancellationToken>()).Returns("24");

        var result = await _sut.GetImdbImportStatusAsync();

        result.LastImportDate.Should().Be(startedAt);
        result.Status.Should().Be("Running");
        result.NextScheduledRun.Should().BeNull(); // FinishedAt is null, so no next scheduled
    }

    [Fact]
    public async Task GetImdbImportStatusAsync_ReturnsDefaults_WhenNoRunExists()
    {
        _importRunRepository.GetLastImportRunAsync(1, Arg.Any<CancellationToken>())
            .Returns((DataSourceImportRun?)null);
        _settingRepository.GetValueAsync("ImdbRefreshIntervalHours", Arg.Any<CancellationToken>()).Returns("24");

        var result = await _sut.GetImdbImportStatusAsync();

        result.LastImportDate.Should().BeNull();
        result.RowsImported.Should().Be(0);
        result.Status.Should().BeNull();
        result.NextScheduledRun.Should().BeNull();
    }

    [Fact]
    public async Task GetImdbImportStatusAsync_NoNextScheduled_WhenRefreshIntervalInvalid()
    {
        var finishedAt = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        var lastRun = new DataSourceImportRun
        {
            ImportRunId = 1,
            SourceId = 1,
            StartedAt = finishedAt.AddMinutes(-30),
            FinishedAt = finishedAt,
            Status = "Complete",
            RowsImported = 50000
        };
        _importRunRepository.GetLastImportRunAsync(1, Arg.Any<CancellationToken>()).Returns(lastRun);
        _settingRepository.GetValueAsync("ImdbRefreshIntervalHours", Arg.Any<CancellationToken>())
            .Returns("invalid");

        var result = await _sut.GetImdbImportStatusAsync();

        result.NextScheduledRun.Should().BeNull();
        result.LastImportDate.Should().Be(finishedAt);
    }

    [Fact]
    public async Task GetImdbImportStatusAsync_NoNextScheduled_WhenRefreshIntervalNull()
    {
        var finishedAt = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        var lastRun = new DataSourceImportRun
        {
            ImportRunId = 1,
            SourceId = 1,
            StartedAt = finishedAt.AddMinutes(-30),
            FinishedAt = finishedAt,
            Status = "Complete",
            RowsImported = 50000
        };
        _importRunRepository.GetLastImportRunAsync(1, Arg.Any<CancellationToken>()).Returns(lastRun);
        _settingRepository.GetValueAsync("ImdbRefreshIntervalHours", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var result = await _sut.GetImdbImportStatusAsync();

        result.NextScheduledRun.Should().BeNull();
    }

    [Fact]
    public async Task GetImdbImportStatusAsync_FailedRun_ReturnsFailedStatus()
    {
        var finishedAt = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        var lastRun = new DataSourceImportRun
        {
            ImportRunId = 1,
            SourceId = 1,
            StartedAt = finishedAt.AddMinutes(-5),
            FinishedAt = finishedAt,
            Status = "Failed",
            RowsImported = 0,
            ErrorMessage = "Connection refused"
        };
        _importRunRepository.GetLastImportRunAsync(1, Arg.Any<CancellationToken>()).Returns(lastRun);
        _settingRepository.GetValueAsync("ImdbRefreshIntervalHours", Arg.Any<CancellationToken>()).Returns("24");

        var result = await _sut.GetImdbImportStatusAsync();

        result.Status.Should().Be("Failed");
        result.RowsImported.Should().Be(0);
        result.NextScheduledRun.Should().Be(finishedAt.AddHours(24));
    }

    [Fact]
    public async Task GetAllSettingsAsync_PassesCancellationToken()
    {
        var cts = new CancellationTokenSource();
        _settingRepository.GetAllAsync(cts.Token).Returns(new List<Setting>());

        await _sut.GetAllSettingsAsync(cts.Token);

        await _settingRepository.Received(1).GetAllAsync(cts.Token);
    }

    [Fact]
    public async Task UpdateSettingAsync_PassesCancellationToken()
    {
        var cts = new CancellationTokenSource();

        await _sut.UpdateSettingAsync("key", "value", cts.Token);

        await _settingRepository.Received(1).UpdateAsync("key", "value", cts.Token);
    }
}
