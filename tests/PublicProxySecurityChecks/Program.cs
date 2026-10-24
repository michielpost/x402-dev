using System.Net;
using x402dev.Server.Security;
using x402dev.Services.Security;

static void Check(bool actual, string name)
{
    if (!actual) throw new Exception("FAIL " + name);
    Console.WriteLine("PASS " + name);
}

string[] blocked =
[
    "file:///etc/passwd", "ftp://example.com/path", "http://localhost/",
    "http://sub.localhost/", "https://app.internal/", "http://host.local/",
    "http://user:pass@example.com/", "http://127.0.0.1:8/",
    "http://127.1:8/", "http://0.0.0.0/", "http://10.2.3.4/",
    "http://100.64.1.1/", "http://169.254.169.254/", "http://172.31.2.3/",
    "http://192.168.1.1/", "http://192.0.2.1/", "http://198.18.0.1/",
    "http://198.51.100.10/", "http://203.0.113.2/", "http://224.0.0.1/",
    "http://[::1]/", "http://[::ffff:127.0.0.1]/", "http://[fe80::1]/",
    "http://[fc00::1]/", "http://[64:ff9b::7f00:1]/",
    "http://[2001:db8::1]/", "http://[2002:c0a8:0101::1]/"
];
foreach (string u in blocked)
{
    Check(!Uri.TryCreate(u, UriKind.Absolute, out var uri) || !PublicProxyNetworkPolicy.IsAllowedUri(uri), "target blocked: " + u);
}
foreach (string u in new[] { "https://example.com/api", "http://1.1.1.1/", "https://[2606:4700:4700::1111]/" })
{
    Check(Uri.TryCreate(u, UriKind.Absolute, out var uri) && PublicProxyNetworkPolicy.IsAllowedUri(uri), "public target accepted: " + u);
}
Check(!PublicProxyNetworkPolicy.AreAllPublicAddresses([]), "empty DNS refused");
Check(!PublicProxyNetworkPolicy.AreAllPublicAddresses([IPAddress.Parse("1.1.1.1"), IPAddress.Loopback]), "mixed public+private DNS refused");
Check(PublicProxyNetworkPolicy.AreAllPublicAddresses([IPAddress.Parse("1.1.1.1"), IPAddress.Parse("8.8.8.8")]), "all-public DNS allowed");

// Ports: only web-facing ports may be reached, so the proxy cannot be used to
// probe unrelated services that happen to listen on a public address.
foreach (int port in new[] { 22, 25, 3306, 5432, 6379, 9200, 11211 })
{
    Check(!PublicProxyNetworkPolicy.IsAllowedPort(port), "port blocked: " + port);
}
foreach (int port in new[] { 80, 443, 8080, 8443 })
{
    Check(PublicProxyNetworkPolicy.IsAllowedPort(port), "port allowed: " + port);
}
foreach (string u in new[] { "http://example.com:22/", "https://example.com:6379/", "http://1.1.1.1:9200/" })
{
    Check(!Uri.TryCreate(u, UriKind.Absolute, out var uri) || !PublicProxyNetworkPolicy.IsAllowedUri(uri), "non-web port blocked: " + u);
}
foreach (string u in new[] { "http://example.com:8080/api", "https://example.com:8443/api" })
{
    Check(Uri.TryCreate(u, UriKind.Absolute, out var uri) && PublicProxyNetworkPolicy.IsAllowedUri(uri), "web port accepted: " + u);
}

// Response headers: a target must not be able to set cookies on the x402dev.com
// origin, smuggle hop-by-hop headers, or override this app's CORS policy.
foreach (string h in new[] { "Set-Cookie", "set-cookie", "SET-COOKIE2", "Transfer-Encoding", "Connection", "Access-Control-Allow-Origin", "Access-Control-Allow-Credentials" })
{
    Check(!ProxyResponseHeaderPolicy.IsForwarded(h), "response header dropped: " + h);
}
foreach (string h in new[] { "Content-Type", "Content-Length", "PAYMENT-REQUIRED", "X-Custom" })
{
    Check(ProxyResponseHeaderPolicy.IsForwarded(h), "response header forwarded: " + h);
}

// Response size ceiling: get-x402 must not buffer an unbounded body.
using (var small = new StringContent("{\"ok\":true}"))
{
    Check(await BoundedContentReader.ReadStringAsync(small) == "{\"ok\":true}", "small body read");
}
using (var atCeiling = new ByteArrayContent(new byte[1024]))
{
    Check(await BoundedContentReader.ReadStringAsync(atCeiling, 1024) is not null, "body at the ceiling accepted");
}
using (var declaredTooLarge = new ByteArrayContent(new byte[2048]))
{
    Check(await BoundedContentReader.ReadStringAsync(declaredTooLarge, 1024) is null, "oversized declared body refused");
}
using (var streamedTooLarge = new StreamContent(new UnknownLengthStream(new byte[4096])))
{
    Check(await BoundedContentReader.ReadStringAsync(streamedTooLarge, 1024) is null, "oversized streamed body refused without Content-Length");
}

using var handler = PublicProxyNetworkPolicy.CreateHandler();
Check(!handler.AllowAutoRedirect, "redirects never followed by the server");
Check(!handler.UseProxy, "environment proxy cannot bypass validated direct socket");
Check(!handler.UseCookies, "remote response cookies are not shared");
Check(handler.ConnectCallback is not null, "outbound socket callback pins inspected IP");
using var client = new HttpClient(handler);
try
{
    await client.GetAsync("http://127.0.0.1:65437/", new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token);
    throw new Exception("FAIL: loopback socket unexpectedly connected");
}
catch (HttpRequestException ex)
{
    Check(ex.ToString().Contains("Unsafe outbound proxy destination", StringComparison.Ordinal), "actual HttpClient localhost connect refused by policy");
}
Console.WriteLine($"ALL OFFLINE PUBLIC-PROXY SECURITY CHECKS PASS: {blocked.Length} denied URL inputs, 3 public cases, 7 boundary checks, 7 blocked ports, 4 allowed ports, 7 dropped headers, 4 forwarded headers, 4 body-size cases. No production/network/external service requested.");

/// <summary>A stream with no discoverable length, so the reader must enforce the ceiling while streaming.</summary>
sealed class UnknownLengthStream(byte[] data) : Stream
{
    private int _position;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var remaining = data.Length - _position;
        if (remaining <= 0) return 0;
        var read = Math.Min(remaining, count);
        Array.Copy(data, _position, buffer, offset, read);
        _position += read;
        return read;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
