using System;

namespace x402dev.Server.Security;

/// <summary>
/// Origin allow-list used with AllowCredentials(). Require real DNS label
/// boundaries rather than string suffixes such as attackerx402dev.com.
/// </summary>
internal static class TrustedCorsOrigins
{
    public static bool IsAllowed(string? origin, bool allowLocalDevelopment)
    {
        if (string.IsNullOrWhiteSpace(origin) ||
            !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            uri.AbsolutePath != "/")
            return false;

        // CORS Origin carries a scheme, host and port, not a path or query.
        // IdnHost normalizes international domain labels to their DNS form.
        var host = uri.IdnHost;

        // Developer-only localhost origins must use their actual loopback host,
        // not a hostname merely *containing* the text "localhost".
        if (allowLocalDevelopment &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
            (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
             host == "127.0.0.1" ||
             host == "::1" || host == "[::1]"))
            return true;

        // Credentialed production calls are allowed only over default HTTPS.
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort)
            return false;

        return InOwnedDomain(host, "x402-dev.pages.dev") ||
               InOwnedDomain(host, "x402dev.com");
    }

    private static bool InOwnedDomain(string host, string root) =>
        host.Equals(root, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith("." + root, StringComparison.OrdinalIgnoreCase);
}
