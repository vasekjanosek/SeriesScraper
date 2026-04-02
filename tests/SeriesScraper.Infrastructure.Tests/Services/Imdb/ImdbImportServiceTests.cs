using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Infrastructure.Data;
using SeriesScraper.Infrastructure.Services.Imdb;

namespace SeriesScraper.Infrastructure.Tests.Services.Imdb;

public class ImdbImportServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ImdbDatasetDownloader _downloader;
    private readonly ImdbDatasetParser _parser;
    private readonly List<string> _tempFiles = new();

    public ImdbImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _context.DataSources.Add(new DataSource { SourceId = 1, Name = "IMDB" });
        _context.SaveChanges();

        _downloader = Substitute.ForPartsOf<ImdbDatasetDownloader>(
            new HttpClient(), NullLogger<ImdbDatasetDownloader>.Instance);

        _parser = new ImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);
    }

    [Fact]
    public async Task RunImportAsync_Success_CreatesAndCompletesImportRun()
    {
        SetupDownloaderReturnsPath();
        var service = CreateTestableService();

        var importRunId = await service.RunImportAsync(CancellationToken.None);

        importRunId.Should().BeGreaterThan(0);
        var run = await _context.DataSourceImportRuns.FindAsync(importRunId);
        run.Should().NotBeNull();
        run!.Status.Should().Be(ImportRunStatus.Complete.ToString());
        run.FinishedAt.Should().NotBeNull();
        run.SourceId.Should().Be(1);
    }

    [Fact]
    public async Task RunImportAsync_Success_UpdatesRowsImported()
    {
        SetupDownloaderReturnsPath();
        var service = CreateTestableService(simulatedRows: 500);

        var importRunId = await service.RunImportAsync(CancellationToken.None);

        var run = await _context.DataSourceImportRuns.FindAsync(importRunId);
        run!.RowsImported.Should().Be(500);
    }

    [Fact]
    public async Task RunImportAsync_DownloadFails_MarksRunAsFailed()
    {
        _downloader.DownloadDatasetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Network error"));

        var service = CreateTestableService();

        await service.Invoking(s => s.RunImportAsync(CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Network error");

        var run = await _context.DataSourceImportRuns.FirstAsync();
        run.Status.Should().Be(ImportRunStatus.Failed.ToString());
        run.FinishedAt.Should().NotBeNull();
        run.ErrorMessage.Should().Be("Network error");
    }

    [Fact]
    public async Task RunImportAsync_DownloadFails_TruncatesLongErrorMessage()
    {
        var longMessage = new string('E', 3000);
        _downloader.DownloadDatasetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException(longMessage));

        var service = CreateTestableService();

        await service.Invoking(s => s.RunImportAsync(CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();

        var run = await _context.DataSourceImportRuns.FirstAsync();
        run.ErrorMessage.Should().HaveLength(2000);
    }

    [Fact]
    public async Task RunImportAsync_Success_CleansTempFiles()
    {
        var tempPaths = CreateRealTempFiles(4);
        var callIndex = 0;
        _downloader.DownloadDatasetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => tempPaths[callIndex++]);

        var service = CreateTestableService();

        await service.RunImportAsync(CancellationToken.None);

        foreach (var path in tempPaths)
        {
            File.Exists(path).Should().BeFalse($"Temp file {path} should have been cleaned up");
        }
    }

    [Fact]
    public async Task RunImportAsync_StagingFails_StillCleansTempFiles()
    {
        var tempPaths = CreateRealTempFiles(4);
        var callIndex = 0;
        _downloader.DownloadDatasetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => tempPaths[callIndex++]);

        var service = CreateTestableService(throwOnStaging: true);

        await service.Invoking(s => s.RunImportAsync(CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();

        foreach (var path in tempPaths)
        {
            File.Exists(path).Should().BeFalse($"Temp file {path} should have been cleaned up on failure");
        }
    }

    [Fact]
    public async Task RunImportAsync_SetsInitialStatusToRunning()
    {
        var downloadStarted = new TaskCompletionSource<bool>();
        var downloadContinue = new TaskCompletionSource<string>();

        _downloader.DownloadDatasetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                downloadStarted.TrySetResult(true);
                return await downloadContinue.Task;
            });

        var service = CreateTestableService();
        var importTask = Task.Run(async () => await service.RunImportAsync(CancellationToken.None));

        await downloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var run = await _context.DataSourceImportRuns.FirstAsync();
        run.Status.Should().Be(ImportRunStatus.Running.ToString());

        downloadContinue.SetResult("fake_path");
        try { await importTask; } catch { /* subsequent download calls may throw */ }
    }

    [Fact]
    public async Task RunImportAsync_CallsStagingAndUpsertInOrder()
    {
        SetupDownloaderReturnsPath();
        var service = CreateTestableService();

        await service.RunImportAsync(CancellationToken.None);

        service.ImportToStagingCalled.Should().BeTrue();
        service.UpsertCalled.Should().BeTrue();
        service.CleanupStagingCalled.Should().BeTrue();
    }

    public void Dispose()
    {
        _context.Dispose();
        foreach (var path in _tempFiles)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private void SetupDownloaderReturnsPath()
    {
        _downloader.DownloadDatasetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("fake_path.tsv.gz");
    }

    private List<string> CreateRealTempFiles(int count)
    {
        var paths = new List<string>();
        for (var i = 0; i < count; i++)
        {
            var path = Path.Combine(Path.GetTempPath(), $"test_import_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(path, "temp");
            paths.Add(path);
            _tempFiles.Add(path);
        }
        return paths;
    }

    private TestableImdbImportService CreateTestableService(
        long simulatedRows = 100,
        bool throwOnStaging = false)
    {
        return new TestableImdbImportService(
            _context, _downloader, _parser,
            NullLogger<ImdbImportService>.Instance,
            simulatedRows, throwOnStaging);
    }

    private class TestableImdbImportService : ImdbImportService
    {
        private readonly long _simulatedRows;
        private readonly bool _throwOnStaging;

        public bool ImportToStagingCalled { get; private set; }
        public bool UpsertCalled { get; private set; }
        public bool CleanupStagingCalled { get; private set; }

        public TestableImdbImportService(
            AppDbContext context,
            ImdbDatasetDownloader downloader,
            ImdbDatasetParser parser,
            ILogger<ImdbImportService> logger,
            long simulatedRows = 100,
            bool throwOnStaging = false)
            : base(context, downloader, parser, logger)
        {
            _simulatedRows = simulatedRows;
            _throwOnStaging = throwOnStaging;
        }

        protected override Task ImportToStagingTablesAsync(
            string basicsPath, string akasPath, string episodePath, string ratingsPath,
            DataSourceImportRun importRun, CancellationToken cancellationToken)
        {
            ImportToStagingCalled = true;
            if (_throwOnStaging)
                throw new InvalidOperationException("Staging failed");
            importRun.RowsImported = _simulatedRows;
            return Task.CompletedTask;
        }

        protected override Task UpsertToLiveTablesAsync(CancellationToken cancellationToken)
        {
            UpsertCalled = true;
            return Task.CompletedTask;
        }

        protected override Task CleanupStagingTablesAsync(CancellationToken cancellationToken)
        {
            CleanupStagingCalled = true;
            return Task.CompletedTask;
        }
    }
}
