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
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void RatingInfo_WithDifferentRating_AreNotEqual()
    {
        var a = new RatingInfo { Rating = 9.0m, VoteCount = 100 };
        var b = new RatingInfo { Rating = 8.5m, VoteCount = 100 };

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void RatingInfo_WithDifferentVoteCount_AreNotEqual()
    {
        var a = new RatingInfo { Rating = 9.0m, VoteCount = 100 };
        var b = new RatingInfo { Rating = 9.0m, VoteCount = 200 };

        a.Should().NotBe(b);
    }

    [Fact]
    public void RatingInfo_GetHashCode_SameForEqualInstances()
    {
        var a = new RatingInfo { Rating = 7.5m, VoteCount = 500 };
        var b = new RatingInfo { Rating = 7.5m, VoteCount = 500 };

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void RatingInfo_GetHashCode_DiffersForDifferentInstances()
    {
        var a = new RatingInfo { Rating = 7.5m, VoteCount = 500 };
        var b = new RatingInfo { Rating = 6.0m, VoteCount = 300 };

        a.GetHashCode().Should().NotBe(b.GetHashCode());
    }

    [Fact]
    public void RatingInfo_ToString_ContainsTypeName()
    {
        var rating = new RatingInfo { Rating = 8.0m, VoteCount = 1000 };

        rating.ToString().Should().Contain("RatingInfo");
    }

    [Fact]
    public void RatingInfo_WithExpression_CreatesModifiedCopy()
    {
        var original = new RatingInfo { Rating = 8.0m, VoteCount = 1000 };
        var modified = original with { VoteCount = 2000 };

        modified.Rating.Should().Be(8.0m);
        modified.VoteCount.Should().Be(2000);
        original.VoteCount.Should().Be(1000);
    }

    [Fact]
    public void RatingInfo_ZeroValues_AreValid()
    {
        var rating = new RatingInfo { Rating = 0m, VoteCount = 0 };

        rating.Rating.Should().Be(0m);
        rating.VoteCount.Should().Be(0);
    }
}
