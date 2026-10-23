using x402dev.Server.Security;

int checks = 0;
static bool OriginalVulnerablePredicate(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
    var host = uri.Host;
    return host == "x402-dev.pages.dev"
        || host.EndsWith("x402-dev.pages.dev")
        || host.EndsWith("x402dev.com")
        || host == "x402dev.com"
        || host.Contains("localhost");
}
void Check(bool value, string desc)
{
    if (!value) throw new Exception("FAIL " + desc);
    checks++;
    Console.WriteLine("PASS " + desc);
}

// Evidence of the original string-boundary flaw (copied verbatim from
// original Program.cs, not a test against any live external host).
foreach (var untrusted in new[]
{
    "https://attackerx402dev.com", "https://notx402-dev.pages.dev",
    "https://my-localhost.example", "http://localhost.evil.example"
})
{
    Check(OriginalVulnerablePredicate(untrusted), "original bad suffix/substring accepted: " + untrusted);
    Check(!TrustedCorsOrigins.IsAllowed(untrusted, false), "production repaired: " + untrusted);
    Check(!TrustedCorsOrigins.IsAllowed(untrusted, true), "development repaired: " + untrusted);
}

foreach (var good in new[]
{
    "https://x402dev.com", "https://www.x402dev.com",
    "https://api.x402dev.com", "https://x402-dev.pages.dev",
    "https://preview.x402-dev.pages.dev", "https://API.X402DEV.COM",
    "https://preview.x402-dev.pages.dev:443"
})
{
    Check(TrustedCorsOrigins.IsAllowed(good, false), "trusted production origin: " + good);
    Check(TrustedCorsOrigins.IsAllowed(good, true), "same origin accepted in dev: " + good);
}

foreach (var bad in new[]
{
    "null", "https://notx402dev.com", "https://x402dev.com.attacker.net",
    "https://x402-dev.pages.dev.attacker.net", "https://evil-x402dev.com",
    "http://x402dev.com", "http://www.x402dev.com",
    "https://x402dev.com:8443", "https://evil.com", "file:///etc/passwd",
    "https://user@x402dev.com", "https://www.x402dev.com/secret",
    "https://www.x402dev.com?a=1", "https://www.x402dev.com#foo",
    "https://localhost", "http://localhost", "http://127.0.0.1:5173",
    "http://[::1]:5173", "http://localhost.localdomain",
    "http://192.168.1.1:5173", "https://localhost.evil.com",
    "https://x402dev.com.evil.com", "https://attackerx402-dev.pages.dev"
})
{
    Check(!TrustedCorsOrigins.IsAllowed(bad, false), "untrusted production origin denied: " + bad);
}

foreach (var dev in new[]
{
    "http://localhost:5173", "https://localhost:7123",
    "http://127.0.0.1:5173", "http://[::1]:5173"
})
{
    Check(TrustedCorsOrigins.IsAllowed(dev, true), "exact developer loopback accepted only in dev: " + dev);
}
foreach (var notLocal in new[]
{
    "http://localhostname.example:5173", "http://evillocalhost.com",
    "http://127.0.0.2:5173", "http://10.0.0.1:5173",
    "http://my-localhost.company", "https://evilx402dev.com"
})
{
    Check(!TrustedCorsOrigins.IsAllowed(notLocal, true), "untrusted developer-mode origin denied: " + notLocal);
}

Console.WriteLine($"PASS ALL {checks} standalone C# boundary-regression checks, actual production guard linked; no live website, network, or wallet used");
