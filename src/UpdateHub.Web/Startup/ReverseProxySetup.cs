using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

namespace UpdateHub.Web.Startup;

/// <summary>
/// X-Forwarded-* headers are honoured only from trusted proxy networks.
/// Trusting everyone would let any client spoof its IP and slip past the
/// per-IP rate limits and IP blocks.
/// </summary>
public static class ReverseProxySetup
{
    public const string ConfigKey = "UpdateHub:TrustedProxies";

    // Loopback + Docker's default bridge range: where a reverse proxy on the
    // same host (e.g. Synology DSM nginx → published port) appears from
    // inside the container. Home LAN ranges are deliberately excluded.
    public static readonly IReadOnlyList<string> DefaultTrustedNetworks =
        ["127.0.0.0/8", "::1/128", "172.16.0.0/12"];

    public static IServiceCollection AddReverseProxySupport(
        this IServiceCollection services, IConfiguration config)
    {
        var networks = ParseNetworks(config[ConfigKey]);

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var network in networks)
                options.KnownIPNetworks.Add(network);
        });

        return services;
    }

    /// <summary>
    /// Parses a comma/semicolon separated CIDR list. Empty → defaults.
    /// Throws on an invalid entry so a typo fails fast at startup instead of
    /// silently trusting nobody (or everybody).
    /// </summary>
    public static IReadOnlyList<IPNetwork> ParseNetworks(string? value)
    {
        var entries = string.IsNullOrWhiteSpace(value)
            ? DefaultTrustedNetworks
            : value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return entries
            .Select(entry => IPNetwork.TryParse(entry, out var network)
                ? network
                : throw new InvalidOperationException(
                    $"{ConfigKey}: '{entry}' is not a valid CIDR network (e.g. 172.16.0.0/12)."))
            .ToList();
    }
}
