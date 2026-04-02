using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeriesScraper.Domain.Entities;

namespace SeriesScraper.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for QualityToken.
/// Demonstrates string enum conversion and seed data per AC#3, #6, #7.
/// Partial index created via raw SQL in migration Up() per AC#1.
/// </summary>
public class QualityTokenConfiguration : IEntityTypeConfiguration<QualityToken>
{
    public void Configure(EntityTypeBuilder<QualityToken> entity)
    {
        entity.HasKey(e => e.TokenId);
        
        entity.Property(e => e.TokenText)
            .IsRequired()
            .HasMaxLength(100);
        
        entity.HasIndex(e => e.TokenText)
            .IsUnique();
        
        // String enum conversion per AC#3
        entity.Property(e => e.Polarity)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);
        
        entity.Property(e => e.IsActive)
            .HasDefaultValue(true);
        
        // Seed data per AC#6
        entity.HasData(
            // Positive polarity
            new QualityToken { TokenId = 1, TokenText = "2160p", QualityRank = 100, Polarity = Domain.Enums.TokenPolarity.Positive, IsActive = true },
            new QualityToken { TokenId = 2, TokenText = "4K", QualityRank = 100, Polarity = Domain.Enums.TokenPolarity.Positive, IsActive = true },
            new QualityToken { TokenId = 3, TokenText = "1080p", QualityRank = 80, Polarity = Domain.Enums.TokenPolarity.Positive, IsActive = true },
            new QualityToken { TokenId = 4, TokenText = "720p", QualityRank = 60, Polarity = Domain.Enums.TokenPolarity.Positive, IsActive = true },
            new QualityToken { TokenId = 5, TokenText = "480p", QualityRank = 40, Polarity = Domain.Enums.TokenPolarity.Positive, IsActive = true },
            new QualityToken { TokenId = 6, TokenText = "BluRay", QualityRank = 70, Polarity = Domain.Enums.TokenPolarity.Positive, IsActive = true },
            new QualityToken { TokenId = 7, TokenText = "WEB-DL", QualityRank = 50, Polarity = Domain.Enums.TokenPolarity.Positive, IsActive = true },
            new QualityToken { TokenId = 8, TokenText = "HEVC", QualityRank = 65, Polarity = Domain.Enums.TokenPolarity.Positive, IsActive = true },
            new QualityToken { TokenId = 9, TokenText = "x265", QualityRank = 65, Polarity = Domain.Enums.TokenPolarity.Positive, IsActive = true },
            new QualityToken { TokenId = 10, TokenText = "x264", QualityRank = 60, Polarity = Domain.Enums.TokenPolarity.Positive, IsActive = true },
            new QualityToken { TokenId = 11, TokenText = "HDR", QualityRank = 75, Polarity = Domain.Enums.TokenPolarity.Positive, IsActive = true },
            new QualityToken { TokenId = 12, TokenText = "SDR", QualityRank = 50, Polarity = Domain.Enums.TokenPolarity.Positive, IsActive = true },
            // Negative polarity
            new QualityToken { TokenId = 13, TokenText = "AI-upscaled", QualityRank = -10, Polarity = Domain.Enums.TokenPolarity.Negative, IsActive = true },
            new QualityToken { TokenId = 14, TokenText = "AI upscale", QualityRank = -10, Polarity = Domain.Enums.TokenPolarity.Negative, IsActive = true }
        );
        
        // NOTE: Partial index IX_QualityTokens_IsActivePartial created via raw SQL in migration Up() per AC#1
    }
}
