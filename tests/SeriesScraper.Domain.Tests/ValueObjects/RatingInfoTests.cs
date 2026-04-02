using FluentAssertions;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Domain.Tests.ValueObjects;

public class RatingInfoTests
{
    [Fact]
    public void RatingInfo_StoresRatingAndVoteCount()
    {
        var rating = new RatingInfo
        {
            Rating = 8.7m,
            VoteCount = 150000
        };

        rating.Rating.Should().Be(8.7m);
        rating.VoteCount.Should().Be(150000);
    }

    [Fact]
    public void RatingInfo_WithSameValues_AreEqual()
    {
        var a = new RatingInfo { Rating = 9.0m, VoteCount = 100 };
        var b = new RatingInfo { Rating = 9.0m, VoteCount = 100 };

        a.Should().Be(b);
    }

    [Fact]
    public void RatingInfo_WithDifferentValues_AreNotEqual()
    {
        var a = new RatingInfo { Rating = 9.0m, VoteCount = 100 };
        var b = new RatingInfo { Rating = 8.5m, VoteCount = 100 };

        a.Should().NotBe(b);
    }
}
