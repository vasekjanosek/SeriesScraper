using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Infrastructure.Data;
using SeriesScraper.Infrastructure.Services.Imdb;

namespace SeriesScraper.Infrastructure.Tests.Services.Imdb;

public class ImdbImportServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ImdbDatasetDownloader _downloader;
    private readonly ImdbDatasetParser _parser;
    private readonly IImdbStagingRepository _stagingRepo;
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
        _stagingRepo = Substitute.For<IImdbStagingRepository>();
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

    // ---- Tests for ImportToStagingTablesAsync ----

    [Fact]
    public async Task ImportToStagingTablesAsync_CallsBulkInsertForAllDatasets()
    {
        var basics = new List<ImdbTitleBasicsStaging> { CreateBasicsEntity() };
        var akas = new List<ImdbTitleAkasStaging> { CreateAkasEntity() };
        var episodes = new List<ImdbTitleEpisodeStaging> { CreateEpisodeEntity() };
        var ratings = new List<ImdbTitleRatingsStaging> { CreateRatingsEntity() };

        var fakeParser = new FakeImdbDatasetParser(
            NullLogger<ImdbDatasetParser>.Instance,
            basicsChunks: [basics],
            akasChunks: [akas],
            episodeChunks: [episodes],
            ratingsChunks: [ratings]);

        var harness = CreateHarness(fakeParser);
        var importRun = await CreateImportRunAsync();

        await harness.CallImportToStagingTablesAsync("a", "b", "c", "d", importRun, CancellationToken.None);

        await _stagingRepo.Received(1).BulkInsertBasicsAsync(basics, Arg.Any<CancellationToken>());
        await _stagingRepo.Received(1).BulkInsertAkasAsync(akas, Arg.Any<CancellationToken>());
        await _stagingRepo.Received(1).BulkInsertEpisodeAsync(episodes, Arg.Any<CancellationToken>());
        await _stagingRepo.Received(1).BulkInsertRatingsAsync(ratings, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportToStagingTablesAsync_TracksRowCountFromBasics()
    {
        var chunk1 = Enumerable.Range(0, 5)
            .Select(i => CreateBasicsEntity($"tt{i:D7}")).ToList();
        var chunk2 = Enumerable.Range(5, 3)
            .Select(i => CreateBasicsEntity($"tt{i:D7}")).ToList();

        var fakeParser = new FakeImdbDatasetParser(
            NullLogger<ImdbDatasetParser>.Instance,
            basicsChunks: [chunk1, chunk2]);

        var harness = CreateHarness(fakeParser);
        var importRun = await CreateImportRunAsync();

        await harness.CallImportToStagingTablesAsync("a", "b", "c", "d", importRun, CancellationToken.None);

        importRun.RowsImported.Should().Be(8);
    }

    [Fact]
    public async Task ImportToStagingTablesAsync_HandlesMultipleChunks()
    {
        var chunk1 = new List<ImdbTitleBasicsStaging> { CreateBasicsEntity("tt0000001") };
        var chunk2 = new List<ImdbTitleBasicsStaging> { CreateBasicsEntity("tt0000002") };

        var fakeParser = new FakeImdbDatasetParser(
            NullLogger<ImdbDatasetParser>.Instance,
            basicsChunks: [chunk1, chunk2]);

        var harness = CreateHarness(fakeParser);
        var importRun = await CreateImportRunAsync();

        await harness.CallImportToStagingTablesAsync("a", "b", "c", "d", importRun, CancellationToken.None);

        await _stagingRepo.Received(2).BulkInsertBasicsAsync(Arg.Any<List<ImdbTitleBasicsStaging>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportToStagingTablesAsync_HandlesEmptyDatasets()
    {
        var fakeParser = new FakeImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance);

        var harness = CreateHarness(fakeParser);
        var importRun = await CreateImportRunAsync();

        await harness.CallImportToStagingTablesAsync("a", "b", "c", "d", importRun, CancellationToken.None);

        importRun.RowsImported.Should().Be(0);
        await _stagingRepo.DidNotReceive().BulkInsertBasicsAsync(Arg.Any<List<ImdbTitleBasicsStaging>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportToStagingTablesAsync_UpdatesProgressAtMilestone()
    {
        // Create exactly 500,000 items to trigger the milestone branch
        var largeChunk = Enumerable.Range(0, 500_000)
            .Select(i => CreateBasicsEntity($"tt{i:D7}")).ToList();

        var fakeParser = new FakeImdbDatasetParser(
            NullLogger<ImdbDatasetParser>.Instance,
            basicsChunks: [largeChunk]);

        var harness = CreateHarness(fakeParser);
        var importRun = await CreateImportRunAsync();

        await harness.CallImportToStagingTablesAsync("a", "b", "c", "d", importRun, CancellationToken.None);

        importRun.RowsImported.Should().Be(500_000);
    }

    [Fact]
    public async Task ImportToStagingTablesAsync_BulkInsertFailure_Propagates()
    {
        var basics = new List<ImdbTitleBasicsStaging> { CreateBasicsEntity() };

        var fakeParser = new FakeImdbDatasetParser(
            NullLogger<ImdbDatasetParser>.Instance,
            basicsChunks: [basics]);

        _stagingRepo.BulkInsertBasicsAsync(Arg.Any<List<ImdbTitleBasicsStaging>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        var harness = CreateHarness(fakeParser);
        var importRun = await CreateImportRunAsync();

        await harness.Invoking(h => h.CallImportToStagingTablesAsync("a", "b", "c", "d", importRun, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Connection failed");
    }

    // ---- Tests for UpsertToLiveTablesAsync ----

    [Fact]
    public async Task UpsertToLiveTablesAsync_DelegatesToStagingRepository()
    {
        var harness = CreateHarness();

        await harness.CallUpsertToLiveTablesAsync(CancellationToken.None);

        await _stagingRepo.Received(1).UpsertToLiveTablesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertToLiveTablesAsync_RepositoryFailure_Propagates()
    {
        _stagingRepo.UpsertToLiveTablesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Upsert failed"));

        var harness = CreateHarness();

        await harness.Invoking(h => h.CallUpsertToLiveTablesAsync(CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Upsert failed");
    }

    // ---- Tests for CleanupStagingTablesAsync ----

    [Fact]
    public async Task CleanupStagingTablesAsync_DelegatesToStagingRepository()
    {
        var harness = CreateHarness();

        await harness.CallCleanupStagingTablesAsync(CancellationToken.None);

        await _stagingRepo.Received(1).CleanupStagingTablesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CleanupStagingTablesAsync_RepositoryFailure_Propagates()
    {
        _stagingRepo.CleanupStagingTablesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Cleanup failed"));

        var harness = CreateHarness();

        await harness.Invoking(h => h.CallCleanupStagingTablesAsync(CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cleanup failed");
    }

    // ---- Full pipeline test with real implementations (not overridden) ----

    [Fact]
    public async Task RunImportAsync_FullPipeline_ExercisesAllStagingRepoMethods()
    {
        SetupDownloaderReturnsPath();

        var basics = new List<ImdbTitleBasicsStaging> { CreateBasicsEntity() };
        var akas = new List<ImdbTitleAkasStaging> { CreateAkasEntity() };
        var episodes = new List<ImdbTitleEpisodeStaging> { CreateEpisodeEntity() };
        var ratings = new List<ImdbTitleRatingsStaging> { CreateRatingsEntity() };

        var fakeParser = new FakeImdbDatasetParser(
            NullLogger<ImdbDatasetParser>.Instance,
            basicsChunks: [basics],
            akasChunks: [akas],
            episodeChunks: [episodes],
            ratingsChunks: [ratings]);

        var service = new ImdbImportService(
            _context, _downloader, fakeParser, _stagingRepo,
            NullLogger<ImdbImportService>.Instance);

        var importRunId = await service.RunImportAsync(CancellationToken.None);

        importRunId.Should().BeGreaterThan(0);
        await _stagingRepo.Received(1).BulkInsertBasicsAsync(basics, Arg.Any<CancellationToken>());
        await _stagingRepo.Received(1).BulkInsertAkasAsync(akas, Arg.Any<CancellationToken>());
        await _stagingRepo.Received(1).BulkInsertEpisodeAsync(episodes, Arg.Any<CancellationToken>());
        await _stagingRepo.Received(1).BulkInsertRatingsAsync(ratings, Arg.Any<CancellationToken>());
        await _stagingRepo.Received(1).UpsertToLiveTablesAsync(Arg.Any<CancellationToken>());
        await _stagingRepo.Received(1).CleanupStagingTablesAsync(Arg.Any<CancellationToken>());

        var run = await _context.DataSourceImportRuns.FindAsync(importRunId);
        run!.Status.Should().Be(ImportRunStatus.Complete.ToString());
        run.RowsImported.Should().Be(1);
    }

    [Fact]
    public async Task RunImportAsync_FullPipeline_StagingRepoFailure_MarksRunAsFailed()
    {
        SetupDownloaderReturnsPath();

        var basics = new List<ImdbTitleBasicsStaging> { CreateBasicsEntity() };
        var fakeParser = new FakeImdbDatasetParser(
            NullLogger<ImdbDatasetParser>.Instance,
            basicsChunks: [basics]);

        _stagingRepo.BulkInsertBasicsAsync(Arg.Any<List<ImdbTitleBasicsStaging>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB connection lost"));

        var service = new ImdbImportService(
            _context, _downloader, fakeParser, _stagingRepo,
            NullLogger<ImdbImportService>.Instance);

        await service.Invoking(s => s.RunImportAsync(CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("DB connection lost");

        var run = await _context.DataSourceImportRuns.FirstAsync();
        run.Status.Should().Be(ImportRunStatus.Failed.ToString());
        run.ErrorMessage.Should().Be("DB connection lost");
    }

    public void Dispose()
    {
        _context.Dispose();
        foreach (var path in _tempFiles)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ---- Helper methods ----

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

    private async Task<DataSourceImportRun> CreateImportRunAsync()
    {
        var importRun = new DataSourceImportRun
        {
            SourceId = 1,
            StartedAt = DateTime.UtcNow,
            Status = ImportRunStatus.Running.ToString(),
            RowsImported = 0
        };
        _context.DataSourceImportRuns.Add(importRun);
        await _context.SaveChangesAsync();
        return importRun;
    }

    private static ImdbTitleBasicsStaging CreateBasicsEntity(string tconst = "tt0000001") => new()
    {
        Tconst = tconst,
        TitleType = "movie",
        PrimaryTitle = "Test Movie",
        OriginalTitle = "Test Movie",
        IsAdult = false,
        StartYear = 2024,
        EndYear = null,
        RuntimeMinutes = 120,
        Genres = "Drama"
    };

    private static ImdbTitleAkasStaging CreateAkasEntity() => new()
    {
        Tconst = "tt0000001",
        Ordering = 1,
        Title = "Test Title",
        Region = "US",
        Language = "en",
        Types = null,
        Attributes = null,
        IsOriginalTitle = true
    };

    private static ImdbTitleEpisodeStaging CreateEpisodeEntity() => new()
    {
        Tconst = "tt0000002",
        ParentTconst = "tt0000001",
        SeasonNumber = 1,
        EpisodeNumber = 1
    };

    private static ImdbTitleRatingsStaging CreateRatingsEntity() => new()
    {
        Tconst = "tt0000001",
        AverageRating = 7.5m,
        NumVotes = 1000
    };

    private ImportServiceHarness CreateHarness(ImdbDatasetParser? parser = null)
    {
        return new ImportServiceHarness(
            _context,
            _downloader,
            parser ?? new FakeImdbDatasetParser(NullLogger<ImdbDatasetParser>.Instance),
            _stagingRepo,
            NullLogger<ImdbImportService>.Instance);
    }

    private TestableImdbImportService CreateTestableService(
        long simulatedRows = 100,
        bool throwOnStaging = false)
    {
        return new TestableImdbImportService(
            _context, _downloader, _parser, _stagingRepo,
            NullLogger<ImdbImportService>.Instance,
            simulatedRows, throwOnStaging);
    }

    /// <summary>
    /// Subclass that overrides all protected methods for isolated RunImportAsync testing.
    /// </summary>
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
            IImdbStagingRepository stagingRepo,
            ILogger<ImdbImportService> logger,
            long simulatedRows = 100,
            bool throwOnStaging = false)
            : base(context, downloader, parser, stagingRepo, logger)
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

    /// <summary>
    /// Exposes protected methods for direct unit testing.
    /// </summary>
    private class ImportServiceHarness : ImdbImportService
    {
        public ImportServiceHarness(
            AppDbContext context,
            ImdbDatasetDownloader downloader,
            ImdbDatasetParser parser,
            IImdbStagingRepository stagingRepo,
            ILogger<ImdbImportService> logger)
            : base(context, downloader, parser, stagingRepo, logger)
        { }

        public Task CallImportToStagingTablesAsync(
            string basicsPath, string akasPath, string episodePath, string ratingsPath,
            DataSourceImportRun importRun, CancellationToken ct)
            => ImportToStagingTablesAsync(basicsPath, akasPath, episodePath, ratingsPath, importRun, ct);

        public Task CallUpsertToLiveTablesAsync(CancellationToken ct)
            => UpsertToLiveTablesAsync(ct);

        public Task CallCleanupStagingTablesAsync(CancellationToken ct)
            => CleanupStagingTablesAsync(ct);
    }

    /// <summary>
    /// Parser subclass that returns predefined chunks instead of reading files.
    /// </summary>
    private class FakeImdbDatasetParser : ImdbDatasetParser
    {
        private readonly List<List<ImdbTitleBasicsStaging>> _basicsChunks;
        private readonly List<List<ImdbTitleAkasStaging>> _akasChunks;
        private readonly List<List<ImdbTitleEpisodeStaging>> _episodeChunks;
        private readonly List<List<ImdbTitleRatingsStaging>> _ratingsChunks;

        public FakeImdbDatasetParser(
            ILogger<ImdbDatasetParser> logger,
            List<List<ImdbTitleBasicsStaging>>? basicsChunks = null,
            List<List<ImdbTitleAkasStaging>>? akasChunks = null,
            List<List<ImdbTitleEpisodeStaging>>? episodeChunks = null,
            List<List<ImdbTitleRatingsStaging>>? ratingsChunks = null)
            : base(logger)
        {
            _basicsChunks = basicsChunks ?? [];
            _akasChunks = akasChunks ?? [];
            _episodeChunks = episodeChunks ?? [];
            _ratingsChunks = ratingsChunks ?? [];
        }

        public override async IAsyncEnumerable<List<ImdbTitleBasicsStaging>> ParseTitleBasicsAsync(
            string gzipPath, int chunkSize = 50_000)
        {
            await Task.CompletedTask;
            foreach (var chunk in _basicsChunks)
                yield return chunk;
        }

        public override async IAsyncEnumerable<List<ImdbTitleAkasStaging>> ParseTitleAkasAsync(
            string gzipPath, int chunkSize = 50_000)
        {
            await Task.CompletedTask;
            foreach (var chunk in _akasChunks)
                yield return chunk;
        }

        public override async IAsyncEnumerable<List<ImdbTitleEpisodeStaging>> ParseTitleEpisodeAsync(
            string gzipPath, int chunkSize = 50_000)
        {
            await Task.CompletedTask;
            foreach (var chunk in _episodeChunks)
                yield return chunk;
        }

        public override async IAsyncEnumerable<List<ImdbTitleRatingsStaging>> ParseTitleRatingsAsync(
            string gzipPath, int chunkSize = 50_000)
        {
            await Task.CompletedTask;
            foreach (var chunk in _ratingsChunks)
                yield return chunk;
        }
    }
}
