using System.Net;
using FluentAssertions;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Tests.ValueObjects;

public class SessionStateTests
{
    [Fact]
    public void IsExpired_WhenExpiresAtUtcInPast_ReturnsTrue()
    {
        var state = new SessionState
        {
            ForumId = 1,
            Cookies = new CookieContainer(),
            CreatedAtUtc = DateTime.UtcNow.AddHours(-2),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
        };

        state.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenExpiresAtUtcInFuture_ReturnsFalse()
    {
        var state = new SessionState
        {
            ForumId = 1,
            Cookies = new CookieContainer(),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        };

        state.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WhenExpiresAtUtcIsNull_ReturnsFalse()
    {
        var state = new SessionState
        {
            ForumId = 1,
            Cookies = new CookieContainer(),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = null
        };

        state.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void ForumId_IsSetCorrectly()
    {
        var state = new SessionState
        {
            ForumId = 42,
            Cookies = new CookieContainer(),
            CreatedAtUtc = DateTime.UtcNow
        };

        state.ForumId.Should().Be(42);
    }

    [Fact]
    public void Cookies_IsSetCorrectly()
    {
        var cookies = new CookieContainer();
        cookies.Add(new Uri("http://example.com"), new Cookie("sid", "abc123"));

        var state = new SessionState
        {
            ForumId = 1,
            Cookies = cookies,
            CreatedAtUtc = DateTime.UtcNow
        };

        state.Cookies.Count.Should().Be(1);
    }

    [Fact]
    public void CreatedAtUtc_IsSetCorrectly()
    {
        var now = DateTime.UtcNow;
        var state = new SessionState
        {
            ForumId = 1,
            Cookies = new CookieContainer(),
            CreatedAtUtc = now
        };

        state.CreatedAtUtc.Should().Be(now);
    }

    [Fact]
    public void Equality_TwoSessionStatesWithSameValues_AreEqual()
    {
        var cookies = new CookieContainer();
        var created = DateTime.UtcNow;
        var expires = created.AddMinutes(30);

        var state1 = new SessionState
        {
            ForumId = 1,
            Cookies = cookies,
            CreatedAtUtc = created,
            ExpiresAtUtc = expires
        };

        var state2 = new SessionState
        {
            ForumId = 1,
            Cookies = cookies,
            CreatedAtUtc = created,
            ExpiresAtUtc = expires
        };

        state1.Should().Be(state2);
    }

    [Fact]
    public void Equality_DifferentForumId_AreNotEqual()
    {
        var cookies = new CookieContainer();
        var created = DateTime.UtcNow;

        var state1 = new SessionState
        {
            ForumId = 1,
            Cookies = cookies,
            CreatedAtUtc = created
        };

        var state2 = new SessionState
        {
            ForumId = 2,
            Cookies = cookies,
            CreatedAtUtc = created
        };

        state1.Should().NotBe(state2);
    }
}
