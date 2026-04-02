using System.IO.Compression;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SeriesScraper.Infrastructure.Services.Imdb;

namespace SeriesScraper.Infrastructure.Tests.Services.Imdb;

public class ImdbDatasetDownloaderTests
{
    [Fact]
    public async Task DownloadDatasetAsync_ValidFile_ReturnsPath()
    {
        // Arrange
        var httpClient = CreateHttpClientWithMockData("title.basics.tsv.gz", CreateValidGzipFile(20_000_000));
        var downloader = new ImdbDatasetDownloader(httpClient, NullLogger<ImdbDatasetDownloader>.Instance);
        
        // Act
        var filePath = await downloader.DownloadDatasetAsync("title.basics.tsv.gz", CancellationToken.None);
        
        // Assert
        filePath.Should().NotBeNullOrEmpty();
        File.Exists(filePath).Should().BeTrue();
        
        // Cleanup
        File.Delete(filePath);
    }
    
    [Fact]
    public async Task DownloadDatasetAsync_InvalidGzipHeader_ThrowsInvalidDataException()
    {
        // Arrange
        var invalidData = new byte[] { 0x00, 0x00, 0x48, 0x65, 0x6c, 0x6c, 0x6f }; // Not a gzip file
        var httpClient = CreateHttpClientWithMockData("title.basics.tsv.gz", invalidData);
        var downloader = new ImdbDatasetDownloader(httpClient, NullLogger<ImdbDatasetDownloader>.Instance);
        
        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(
            () => downloader.DownloadDatasetAsync("title.basics.tsv.gz", CancellationToken.None));
    }
    
    [Fact]
    public async Task DownloadDatasetAsync_TooFewRows_ThrowsInvalidDataException()
    {
        // Arrange - Create gzip with only 100 rows (minimum is 10M for title.basics)
        var httpClient = CreateHttpClientWithMockData("title.basics.tsv.gz", CreateValidGzipFile(100));
        var downloader = new ImdbDatasetDownloader(httpClient, NullLogger<ImdbDatasetDownloader>.Instance);
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => downloader.DownloadDatasetAsync("title.basics.tsv.gz", CancellationToken.None));
        
        exception.Message.Should().Contain("truncated or corrupted");
    }
    
    [Fact]
    public async Task DownloadDatasetAsync_HttpError_ThrowsHttpRequestException()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.NotFound, Array.Empty<byte>());
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://datasets.imdbapi.com/") };
        var downloader = new ImdbDatasetDownloader(httpClient, NullLogger<ImdbDatasetDownloader>.Instance);
        
        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => downloader.DownloadDatasetAsync("nonexistent.tsv.gz", CancellationToken.None));
    }
    
    private static HttpClient CreateHttpClientWithMockData(string filename, byte[] data)
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, data);
        return new HttpClient(handler) { BaseAddress = new Uri("https://datasets.imdbapi.com/") };
    }
    
    private static byte[] CreateValidGzipFile(int rowCount)
    {
        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Fastest))
        using (var writer = new StreamWriter(gzipStream))
        {
            // Write TSV header
            writer.WriteLine("tconst\ttitleType\tprimaryTitle\toriginalTitle\tisAdult\tstartYear\tendYear\truntimeMinutes\tgenres");
            
            // Write data rows
            for (int i = 0; i < rowCount; i++)
            {
                writer.WriteLine($"tt{i:D7}\tmovie\tTitle {i}\t\\N\t0\t2020\t\\N\t120\tAction,Drama");
            }
        }
        
        return memoryStream.ToArray();
    }
    
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly byte[] _content;
        
        public MockHttpMessageHandler(HttpStatusCode statusCode, byte[] content)
        {
            _statusCode = statusCode;
            _content = content;
        }
        
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new ByteArrayContent(_content)
            };
            
            return Task.FromResult(response);
        }
    }
}
