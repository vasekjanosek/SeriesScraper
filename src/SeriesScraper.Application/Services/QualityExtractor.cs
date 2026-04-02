using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Entities;
using SeriesScraper.Domain.Enums;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Domain.ValueObjects;

namespace SeriesScraper.Application.Services;

/// <summary>
/// Extracts quality information from forum post text by scanning for known tokens,
/// evaluating learned regex patterns, and proposing new patterns for learning.
/// </summary>
public class QualityExtractor : IQualityExtractor
{
    public const string AlgorithmVersion = "1.0";

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Heuristic patterns used to discover potential quality tokens not yet in the database.
    /// Each pattern maps to a default derived rank and polarity.
    /// </summary>
    private static readonly IReadOnlyList<(Regex Pattern, int DefaultRank, TokenPolarity Polarity)> DiscoveryPatterns =
    [
        (new Regex(@"\b(\d{3,4}p)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout), 50, TokenPolarity.Positive),
        (new Regex(@"\b(x\d{3})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout), 50, TokenPolarity.Positive),
        (new Regex(@"\b(H\.?\d{3})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout), 50, TokenPolarity.Positive),
        (new Regex(@"\b(DTS[-\s]?HD)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout), 50, TokenPolarity.Positive),
        (new Regex(@"\b(Atmos)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout), 50, TokenPolarity.Positive),
        (new Regex(@"\b(Remux)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout), 50, TokenPolarity.Positive),
    ];

    private readonly IQualityPatternService _patternService;
    private readonly ILogger<QualityExtractor> _logger;

    public QualityExtractor(IQualityPatternService patternService, ILogger<QualityExtractor> logger)
    {
        _patternService = patternService ?? throw new ArgumentNullException(nameof(patternService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<QualityRank> ExtractAsync(string postText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(postText))
            return QualityRank.None;

        var activeTokens = await _patternService.GetActiveTokensAsync(ct);
        var activePatterns = await _patternService.GetActivePatternsAsync(ct);

        var matches = new List<QualityMatch>();

        // AC#1: Evaluate all active QualityTokens (case-insensitive word boundary match)
        EvaluateTokens(postText, activeTokens, matches);

        // AC#1: Evaluate all active QualityLearnedPatterns (regex match)
        EvaluatePatterns(postText, activePatterns, matches);

        // AC#3: Increment hit_count on matched patterns
        await RecordPatternHitsAsync(matches, ct);

        // AC#4: Discover and record new patterns from the post text
        await DiscoverNewPatternsAsync(postText, activeTokens, activePatterns, ct);

        if (matches.Count == 0)
            return QualityRank.None;

        // AC#2: Select best match — negative polarity downranked below any positive
        return RankMatches(matches);
    }

    private void EvaluateTokens(string postText, IReadOnlyList<QualityToken> tokens, List<QualityMatch> matches)
    {
        foreach (var token in tokens)
        {
            if (ContainsToken(postText, token.TokenText))
            {
                matches.Add(new QualityMatch
                {
                    MatchedText = token.TokenText,
                    Rank = token.QualityRank,
                    Polarity = token.Polarity,
                    Source = MatchSource.Token,
                    PatternId = null
                });
            }
        }
    }

    private void EvaluatePatterns(string postText, IReadOnlyList<QualityLearnedPattern> patterns, List<QualityMatch> matches)
    {
        foreach (var pattern in patterns)
        {
            // AC#5: Compile with timeout, catch RegexMatchTimeoutException
            if (TryRegexMatch(postText, pattern.PatternRegex, out var matchedText))
            {
                matches.Add(new QualityMatch
                {
                    MatchedText = matchedText!,
                    Rank = pattern.DerivedRank,
                    Polarity = pattern.Polarity,
                    Source = MatchSource.Pattern,
                    PatternId = pattern.PatternId
                });
            }
        }
    }

    private bool TryRegexMatch(string text, string pattern, out string? matchedText)
    {
        matchedText = null;
        try
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase, RegexTimeout);
            var match = regex.Match(text);
            if (match.Success)
            {
                matchedText = match.Value;
                return true;
            }
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            // AC#5: Log at WARN level with pattern text, treat as no-match
            _logger.LogWarning("Regex pattern timed out after {Timeout}s: {Pattern}", RegexTimeout.TotalSeconds, pattern);
            return false;
        }
    }

    private async Task RecordPatternHitsAsync(List<QualityMatch> matches, CancellationToken ct)
    {
        // AC#3: Increment hit_count via single update statement (handled by repository)
        foreach (var match in matches.Where(m => m.Source == MatchSource.Pattern && m.PatternId.HasValue))
        {
            await _patternService.RecordPatternHitAsync(match.PatternId!.Value, ct);
        }
    }

    private async Task DiscoverNewPatternsAsync(
        string postText,
        IReadOnlyList<QualityToken> knownTokens,
        IReadOnlyList<QualityLearnedPattern> knownPatterns,
        CancellationToken ct)
    {
        // AC#4: Find potential quality tokens not in the database
        var knownTokenTexts = new HashSet<string>(
            knownTokens.Select(t => t.TokenText),
            StringComparer.OrdinalIgnoreCase);

        var knownPatternTexts = new HashSet<string>(
            knownPatterns.Select(p => p.PatternRegex),
            StringComparer.OrdinalIgnoreCase);

        foreach (var (discoveryPattern, defaultRank, polarity) in DiscoveryPatterns)
        {
            MatchCollection regexMatches;
            try
            {
                regexMatches = discoveryPattern.Matches(postText);
            }
            catch (RegexMatchTimeoutException)
            {
                _logger.LogWarning("Discovery pattern timed out: {Pattern}", discoveryPattern.ToString());
                continue;
            }

            foreach (Match regexMatch in regexMatches)
            {
                var observed = regexMatch.Value;
                if (knownTokenTexts.Contains(observed))
                    continue;

                // Build the pattern regex for the discovered token
                var newPatternRegex = $@"\b{Regex.Escape(observed)}\b";
                if (knownPatternTexts.Contains(newPatternRegex))
                    continue;

                // AC#4: Record as new learned pattern
                var newPattern = await _patternService.AddLearnedPatternAsync(
                    newPatternRegex, defaultRank, polarity, AlgorithmVersion, ct);

                // Immediately record initial hit
                await _patternService.RecordPatternHitAsync(newPattern.PatternId, ct);

                // Track so we don't add duplicates within the same post
                knownPatternTexts.Add(newPatternRegex);
            }
        }
    }

    private static QualityRank RankMatches(List<QualityMatch> matches)
    {
        var positiveMatches = matches.Where(m => m.Polarity == TokenPolarity.Positive).ToList();
        var negativeMatches = matches.Where(m => m.Polarity == TokenPolarity.Negative).ToList();

        QualityMatch bestMatch;

        // AC#2: If any positive match exists, negative polarity is downranked below all positive
        if (positiveMatches.Count > 0)
        {
            bestMatch = positiveMatches.OrderByDescending(m => m.Rank).First();
        }
        else
        {
            bestMatch = negativeMatches.OrderByDescending(m => m.Rank).First();
        }

        var allMatchedTokens = matches.Select(m => m.MatchedText).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Confidence: scales with match count, approaches 1.0 asymptotically
        var confidence = allMatchedTokens.Count / (allMatchedTokens.Count + 1.0);

        return new QualityRank
        {
            Score = bestMatch.Rank,
            MatchedTokens = allMatchedTokens,
            Confidence = Math.Round(confidence, 4),
            BestMatchPolarity = bestMatch.Polarity
        };
    }

    private static bool ContainsToken(string text, string token)
    {
        // Case-insensitive word boundary match using IndexOf for performance
        var index = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            var before = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var afterPos = index + token.Length;
            var after = afterPos >= text.Length || !char.IsLetterOrDigit(text[afterPos]);

            if (before && after)
                return true;

            index = text.IndexOf(token, index + 1, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private enum MatchSource { Token, Pattern }

    private sealed class QualityMatch
    {
        public required string MatchedText { get; init; }
        public required int Rank { get; init; }
        public required TokenPolarity Polarity { get; init; }
        public required MatchSource Source { get; init; }
        public int? PatternId { get; init; }
    }
}
