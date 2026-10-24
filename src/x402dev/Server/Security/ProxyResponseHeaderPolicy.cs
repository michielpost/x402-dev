namespace x402dev.Server.Security;

/// <summary>
/// Response headers that must never be relayed from a proxied target to the
/// browser. Set-Cookie would let any target plant cookies on the x402dev.com
/// origin; the rest are hop-by-hop headers or would override this app's own
/// CORS policy with values chosen by the target.
/// </summary>
internal static class ProxyResponseHeaderPolicy
{
    private static readonly HashSet<string> Dropped = new(StringComparer.OrdinalIgnoreCase)
    {
        "Set-Cookie",
        "Set-Cookie2",
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailer",
        "Transfer-Encoding",
        "Upgrade",
        "Access-Control-Allow-Origin",
        "Access-Control-Allow-Credentials",
        "Access-Control-Allow-Methods",
        "Access-Control-Allow-Headers",
        "Access-Control-Expose-Headers",
    };

    public static bool IsForwarded(string headerName) => !Dropped.Contains(headerName);
}
