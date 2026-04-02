using FluentAssertions;
using SeriesScraper.Domain.Exceptions;

namespace SeriesScraper.Domain.Tests.Exceptions;

public class ScrapingExceptionTests
{
    [Fact]
    public void ScrapingException_DefaultConstructor_HasNoMessage()
    {
        var ex = new ScrapingException();

        ex.Message.Should().NotBeNullOrEmpty(); // Default .NET message
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void ScrapingException_WithMessage_StoresMessage()
    {
        var ex = new ScrapingException("Connection timed out");

        ex.Message.Should().Be("Connection timed out");
    }

    [Fact]
    public void ScrapingException_WithInnerException_StoresBoth()
    {
        var inner = new InvalidOperationException("inner error");
        var ex = new ScrapingException("Scraping failed", inner);

        ex.Message.Should().Be("Scraping failed");
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void ScrapingException_IsException()
    {
        var ex = new ScrapingException("test");

        ex.Should().BeAssignableTo<Exception>();
    }
}
