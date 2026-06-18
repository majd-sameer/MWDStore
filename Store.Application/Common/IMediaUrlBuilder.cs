namespace Store.Application.Common;

/// <summary>
/// Maps a stored <c>Medium.FileName</c> to a browser-reachable URL. The default implementation
/// matches the API host's local-disk storage convention (<c>/user-content/{fileName}</c>); swap it
/// for a CDN/S3 builder without touching the catalog/cart read paths.
/// </summary>
public interface IMediaUrlBuilder
{
    string? GetUrl(string? fileName);
}

public sealed class LocalMediaUrlBuilder : IMediaUrlBuilder
{
    public string? GetUrl(string? fileName) =>
        string.IsNullOrWhiteSpace(fileName) ? null
        : IsAbsoluteUrl(fileName) ? fileName
        : $"/user-content/{fileName}";

    /// <summary>Seeded media may store an absolute external URL instead of a local file name.</summary>
    public static bool IsAbsoluteUrl(string fileName) =>
        fileName.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || fileName.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
