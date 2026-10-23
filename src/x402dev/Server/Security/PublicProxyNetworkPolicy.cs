using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace x402dev.Server.Security;

/// <summary>
/// Fail-closed outbound connection policy for the public x402 request proxy.
/// The actual socket uses the IP addresses classified here, not a second DNS lookup:
/// a pre-flight DNS check followed by the normal HttpClient resolver is rebindable.
/// </summary>
internal static class PublicProxyNetworkPolicy
{
    public const string ClientName = "public-x402-proxy";

    public static bool IsAllowedUri(Uri? uri) =>
        uri is { IsAbsoluteUri: true } &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        IsAllowedHost(uri.Host);

    public static SocketsHttpHandler CreateHandler() => new()
    {
        // Never follow an uninspected redirect. A 3xx is returned to the calling
        // client without server-side fetching the Location value.
        AllowAutoRedirect = false,
        // A configured environment/system HTTP proxy would bypass the pinned
        // target socket check by forwarding private URLs from a public proxy.
        UseProxy = false,
        UseCookies = false,
        ConnectCallback = ConnectPublicAddressAsync,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    };

    public static bool IsAllowedHost(string host)
    {
        var name = host.Trim().Trim('[', ']').TrimEnd('.');
        if (name.Length == 0 || name.Contains('%') ||
            name.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".test", StringComparison.OrdinalIgnoreCase))
            return false;

        // IP literals, including IPv4-mapped IPv6 and numeric legacy IPv4
        // notations parsed by IPAddress.TryParse, must pass the same policy.
        return !IPAddress.TryParse(name, out var literal) || IsPublicAddress(literal);
    }

    public static bool AreAllPublicAddresses(IReadOnlyList<IPAddress> addresses) =>
        addresses.Count > 0 && addresses.All(IsPublicAddress);

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) return IsPublicAddress(address.MapToIPv4());
        if (IPAddress.IsLoopback(address)) return false;

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            int a = bytes[0], b = bytes[1], c = bytes[2];
            return !(a == 0 || a == 10 || a == 127 || a >= 224 ||
                (a == 100 && b >= 64 && b <= 127) ||
                (a == 169 && b == 254) ||
                (a == 172 && b >= 16 && b <= 31) ||
                (a == 192 && b == 0 && c == 0) ||
                (a == 192 && b == 0 && c == 2) ||
                (a == 192 && b == 88 && c == 99) ||
                (a == 192 && b == 168) ||
                (a == 198 && (b == 18 || b == 19 || (b == 51 && c == 100))) ||
                (a == 203 && b == 0 && c == 113));
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6 ||
            address.IsIPv6LinkLocal || address.IsIPv6SiteLocal ||
            address.IsIPv6Multicast || address.IsIPv6Teredo)
            return false;

        // IPv6 must be global unicast (2000::/3), excluding documented and
        // translated/tunneled blocks. Rejecting NAT64 (64:ff9b::/96) outright
        // also prevents translated loopback/private IPv4 destinations.
        if ((bytes[0] & 0xE0) != 0x20) return false;
        if (bytes[0] == 0x20 && bytes[1] == 0x02) return false; // 6to4
        if (bytes[0] == 0x20 && bytes[1] == 0x01)
        {
            if (bytes[2] == 0x0d && bytes[3] == 0xb8) return false; // documentation
            if (bytes[2] == 0x00 && bytes[3] == 0x00) return false; // Teredo
            if (bytes[2] == 0x00 && bytes[3] == 0x02) return false; // benchmarking
            if (bytes[2] == 0x00 && (bytes[3] & 0xf0) == 0x10) return false; // ORCHID
        }
        if (bytes[0] == 0x3f && bytes[1] == 0xff && (bytes[2] & 0xf0) == 0)
            return false; // RFC 9637 documentation block
        return true;
    }

    private static async ValueTask<Stream> ConnectPublicAddressAsync(
        SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var endpoint = context.DnsEndPoint;
        if (endpoint.Port is < 1 or > 65535 || !IsAllowedHost(endpoint.Host))
            throw new HttpRequestException("Unsafe outbound proxy destination.");

        IPAddress[] resolved;
        try
        {
            resolved = await Dns.GetHostAddressesAsync(endpoint.Host.Trim('[', ']'), cancellationToken);
        }
        catch (Exception ex) when (ex is SocketException or ArgumentException)
        {
            throw new HttpRequestException("Unable to verify outbound proxy DNS target.", ex);
        }

        // Do not choose a public address if the same hostname has even one
        // private/mixed A or AAAA answer. Every socket must use an IP from
        // this validated set; no second resolution/rebinding is possible.
        if (!AreAllPublicAddresses(resolved))
            throw new HttpRequestException("Unsafe outbound proxy destination address.");

        Exception? lastError = null;
        foreach (var address in resolved)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, endpoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException)
            {
                socket.Dispose();
                lastError = ex;
                if (cancellationToken.IsCancellationRequested) throw;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
        throw new HttpRequestException("Public proxy destination connection failed.", lastError);
    }
}
