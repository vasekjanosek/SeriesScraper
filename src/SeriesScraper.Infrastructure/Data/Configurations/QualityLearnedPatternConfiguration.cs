using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for QualityLearnedPattern.
/// String enum conversions for Polarity and Source per AC#2.
/// Seed data and partial index per AC#3, AC#6.
/// </summary>
public class QualityLearnedPatternConfiguration : IEntityTypeConfiguration<QualityLearnedPattern>
{
    public void Configure(EntityTypeBuilder<QualityLearnedPattern> entity)
    {
        entity.HasKey(e => e.PatternId);

        entity.Property(e => e.PatternRegex)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(e => e.HitCount)
            .HasDefaultValue(0);

        entity.Property(e => e.IsActive)
            .HasDefaultValue(true);

        entity.Property(e => e.LastMatchedAt)
            .HasColumnType("timestamp with time zone");

        entity.Property(e => e.AlgorithmVersion)
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.Polarity)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.Source)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        // Seed data per AC#6 — initial regex patterns derived from quality tokens
        entity.HasData(
            new QualityLearnedPattern
            {
                PatternId = 1,
                PatternRegex = @"\b2160p\b",
                DerivedRank = 100,
                HitCount = 0,
                IsActive = true,
                AlgorithmVersion = "1.0",
                Polarity = TokenPolarity.Positive,
                Source = PatternSource.Seed
            },
            new QualityLearnedPattern
            {
                PatternId = 2,
                PatternRegex = @"\b4K\b",
                DerivedRank = 100,
                HitCount = 0,
                IsActive = true,
                AlgorithmVersion = "1.0",
                Polarity = TokenPolarity.Positive,
                Source = PatternSource.Seed
            },
            new QualityLearnedPattern
            {
                PatternId = 3,
                PatternRegex = @"\b1080p\b",
                DerivedRank = 80,
                HitCount = 0,
                IsActive = true,
                AlgorithmVersion = "1.0",
                Polarity = TokenPolarity.Positive,
                Source = PatternSource.Seed
            },
            new QualityLearnedPattern
            {
                PatternId = 4,
                PatternRegex = @"\b720p\b",
                DerivedRank = 60,
                HitCount = 0,
                IsActive = true,
                AlgorithmVersion = "1.0",
                Polarity = TokenPolarity.Positive,
                Source = PatternSource.Seed
            },
            new QualityLearnedPattern
            {
                PatternId = 5,
                PatternRegex = @"\bBluRay\b",
                DerivedRank = 70,
                HitCount = 0,
                IsActive = true,
                AlgorithmVersion = "1.0",
                Polarity = TokenPolarity.Positive,
                Source = PatternSource.Seed
            },
            new QualityLearnedPattern
            {
                PatternId = 6,
                PatternRegex = @"\bWEB[-\s]?DL\b",
                DerivedRank = 50,
                HitCount = 0,
                IsActive = true,
                AlgorithmVersion = "1.0",
                Polarity = TokenPolarity.Positive,
                Source = PatternSource.Seed
            },
            new QualityLearnedPattern
            {
                PatternId = 7,
                PatternRegex = @"\bHEVC\b",
                DerivedRank = 65,
                HitCount = 0,
                IsActive = true,
                AlgorithmVersion = "1.0",
                Polarity = TokenPolarity.Positive,
                Source = PatternSource.Seed
            },
            new QualityLearnedPattern
            {
                PatternId = 8,
                PatternRegex = @"\bx265\b",
                DerivedRank = 65,
                HitCount = 0,
                IsActive = true,
                AlgorithmVersion = "1.0",
                Polarity = TokenPolarity.Positive,
                Source = PatternSource.Seed
            },
            new QualityLearnedPattern
            {
                PatternId = 9,
                PatternRegex = @"\bAI[-\s]?upscale[d]?\b",
                DerivedRank = -10,
                HitCount = 0,
                IsActive = true,
                AlgorithmVersion = "1.0",
                Polarity = TokenPolarity.Negative,
                Source = PatternSource.Seed
            }
        );

        // NOTE: Partial index IX_QualityLearnedPatterns_IsActivePartial
        // created via raw SQL in migration Up() per AC#3
    }
}
