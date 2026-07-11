namespace Store.Api.Infrastructure;

/// <summary>
/// Download seam for <see cref="MediaSeeder"/>'s URL-bootstrap source, kept as a tiny interface so
/// tests can fake network access instead of hitting the real PSD site.
/// </summary>
public interface IMediaDownloader
{
    /// <summary>Downloads <paramref name="url"/> and returns its bytes, or <c>null</c> on any
    /// failure (non-2xx, timeout, DNS/connect error, ...). Never throws — a fresh environment with
    /// no internet access must still boot with imageless products rather than crash.</summary>
    Task<byte[]?> DownloadAsync(string url, CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="IMediaDownloader"/> backed by a real <see cref="HttpClient"/> (registered as a typed
/// client in Program.cs with a ~15s timeout). No retries — a single failure is logged by the caller
/// and the product is simply skipped until the next boot.
/// </summary>
public sealed class HttpMediaDownloader : IMediaDownloader
{
    private readonly HttpClient _httpClient;

    public HttpMediaDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<byte[]?> DownloadAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            // Network errors, per-request timeouts, bad hosts, etc. — treated as "no image
            // available". A TaskCanceledException caused by the caller's own cancellation (app
            // shutdown), rather than the HttpClient's internal timeout, is left to propagate.
            if (ex is TaskCanceledException && cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return null;
        }
    }
}
