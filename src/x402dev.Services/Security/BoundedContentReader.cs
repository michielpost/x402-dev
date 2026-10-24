using System.Net.Http;
using System.Text;

namespace x402dev.Services.Security;

/// <summary>
/// Reads an HTTP response body with a hard byte ceiling so a hostile target
/// cannot exhaust server memory by returning an unbounded payload.
/// </summary>
public static class BoundedContentReader
{
    public const int DefaultMaxBytes = 1024 * 1024;

    /// <summary>
    /// Returns the decoded body, or <c>null</c> when the body is larger than
    /// <paramref name="maxBytes"/>. The stream is read incrementally, so an
    /// oversized body is abandoned as soon as the ceiling is crossed.
    /// </summary>
    public static async Task<string?> ReadStringAsync(
        HttpContent content,
        int maxBytes = DefaultMaxBytes,
        CancellationToken cancellationToken = default)
    {
        if (content.Headers.ContentLength is long declared && declared > maxBytes)
            return null;

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes) return null;
            buffer.Write(chunk, 0, read);
        }

        return ResolveEncoding(content.Headers.ContentType?.CharSet)
            .GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static Encoding ResolveEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset)) return Encoding.UTF8;
        try
        {
            return Encoding.GetEncoding(charset.Trim('"', ' '));
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
        catch (NotSupportedException)
        {
            return Encoding.UTF8;
        }
    }
}
