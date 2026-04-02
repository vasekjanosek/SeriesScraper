using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using SeriesScraper.Domain.Interfaces;

namespace SeriesScraper.Application.Services;

/// <summary>
/// Validates forum URLs to prevent Server-Side Request Forgery (SSRF) attacks.
/// Blocks private/reserved IP ranges, loopback addresses, and dangerous URI schemes.
/// </summary>
public sealed class ForumUrlValidator : IUrlValidator
{
    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https"
    };

    private readonly ILogger<ForumUrlValidator> _logger;

    public ForumUrlValidator(ILogger<ForumUrlValidator> logger)
    {
        _logger = logger;
    }

    public bool IsUrlSafe(string url)
        => IsUrlSafe(url, out _);

    public bool IsUrlSafe(string url, out string? reason)
    {
        reason = null;

        if (string.IsNullOrWhiteSpace(url))
        {
            reason = "URL is empty or whitespace.";
            return false;
        }

        // Parse URI
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            reason = "URL is not a valid absolute URI.";
            return false;
        }

        // Check scheme
        if (!AllowedSchemes.Contains(uri.Scheme))
        {
            reason = $"Scheme '{uri.Scheme}' is not allowed. Only http and https are permitted.";
            _logger.LogWarning("SSRF blocked: {Reason} URL: {Url}", reason, url);
            return false;
        }

        // Check for localhost hostnames
        var host = uri.Host;
        if (IsLocalhostHostname(host))
        {
            reason = $"Host '{host}' resolves to localhost and is blocked.";
            _logger.LogWarning("SSRF blocked: {Reason} URL: {Url}", reason, url);
            return false;
        }

        // Check if the host is an IP address directly
        if (IPAddress.TryParse(host, out var ipAddress))
        {
            if (IsBlockedIpAddress(ipAddress))
            {
                reason = $"IP address '{ipAddress}' is in a blocked range (private/reserved).";
                _logger.LogWarning("SSRF blocked: {Reason} URL: {Url}", reason, url);
                return false;
            }
        }

        // DNS resolution check to catch DNS rebinding (defense-in-depth)
        try
        {
            var addresses = Dns.GetHostAddresses(host);
            foreach (var addr in addresses)
            {
                if (IsBlockedIpAddress(addr))
                {
                    reason = $"Host '{host}' resolved to blocked IP address '{addr}'.";
                    _logger.LogWarning("SSRF blocked: {Reason} URL: {Url}", reason, url);
                    return false;
                }
            }
        }
        catch (SocketException)
        {
            // DNS resolution failure is not a block — the domain may be valid but
            // unresolvable in the current network context (e.g., tests, CI).
            // Primary SSRF protection is the IP/scheme/hostname checks above.
            _logger.LogDebug("DNS resolution failed for host '{Host}', allowing URL as primary checks passed. URL: {Url}", host, url);
        }

        return true;
    }

    private static bool IsLocalhostHostname(string host)
    {
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "localhost.localdomain", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsBlockedIpAddress(IPAddress address)
    {
        // Normalize IPv6-mapped IPv4 addresses
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        // IPv6 loopback (::1)
        if (IPAddress.IsLoopback(address))
            return true;

        // IPv6 link-local (fe80::/10)
        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv6LinkLocal)
            return true;

        // IPv6 site-local (deprecated fec0::/10)
        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv6SiteLocal)
            return true;

        // IPv6 unique local addresses (fc00::/7)
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC) // fc00::/7
                return true;
        }

        if (address.AddressFamily != AddressFamily.InterNetwork)
            return false;

        var ipBytes = address.GetAddressBytes();

        // 127.0.0.0/8 (loopback)
        if (ipBytes[0] == 127)
            return true;

        // 10.0.0.0/8 (private)
        if (ipBytes[0] == 10)
            return true;

        // 172.16.0.0/12 (private)
        if (ipBytes[0] == 172 && ipBytes[1] >= 16 && ipBytes[1] <= 31)
            return true;

        // 192.168.0.0/16 (private)
        if (ipBytes[0] == 192 && ipBytes[1] == 168)
            return true;

        // 169.254.0.0/16 (link-local / APIPA)
        if (ipBytes[0] == 169 && ipBytes[1] == 254)
            return true;

        // 0.0.0.0/8 (current network)
        if (ipBytes[0] == 0)
            return true;

        // 100.64.0.0/10 (carrier-grade NAT)
        if (ipBytes[0] == 100 && ipBytes[1] >= 64 && ipBytes[1] <= 127)
            return true;

        // 198.18.0.0/15 (benchmarking)
        if (ipBytes[0] == 198 && (ipBytes[1] == 18 || ipBytes[1] == 19))
            return true;

        // 224.0.0.0/4 (multicast)
        if (ipBytes[0] >= 224 && ipBytes[0] <= 239)
            return true;

        // 240.0.0.0/4 (reserved)
        if (ipBytes[0] >= 240)
            return true;

        return false;
    }
}
