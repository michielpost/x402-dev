using System.Net;
using x402dev.Server.Security;

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
Console.WriteLine($"ALL OFFLINE PUBLIC-PROXY SECURITY CHECKS PASS: {blocked.Length} denied URL inputs, 3 public cases, 7 boundary checks. No production/network/external service requested.");
