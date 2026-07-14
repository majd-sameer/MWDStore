namespace Store.Api.Infrastructure;

/// <summary>
/// <see cref="Store.Domain.Medium.MediaType"/> is stored as a plain <see cref="int"/>; these
/// constants keep the values compatible with migrated data.
/// </summary>
public static class MediaTypes
{
    public const int Image = 1;
    public const int File = 5;
    public const int Video = 10;
}

/// <summary>
/// Local-disk media storage. Files live under
/// <c>{ContentRoot}/user-content</c> and are served by the static-files middleware at
/// <c>/user-content/{fileName}</c> (see Program.cs).
/// </summary>
public interface IMediaStorage
{
    Task<string> SaveAsync(Stream stream, string originalFileName, CancellationToken cancellationToken = default);
    void Delete(string? fileName);
    string? GetUrl(string? fileName);
}

public sealed class LocalMediaStorage : IMediaStorage
{
    public const string RequestPath = "/user-content";
    public const string FolderName = "user-content";

    private readonly string _rootPath;

    public LocalMediaStorage(IWebHostEnvironment environment)
    {
        _rootPath = Path.Combine(environment.ContentRootPath, FolderName);
        Directory.CreateDirectory(_rootPath);
    }

    /// <summary>Saves under a GUID name (keeps the original extension) and returns that name.</summary>
    public async Task<string> SaveAsync(Stream stream, string originalFileName, CancellationToken cancellationToken = default)
    {
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(originalFileName)}";
        var filePath = Path.Combine(_rootPath, fileName);
        await using var output = File.Create(filePath);
        await stream.CopyToAsync(output, cancellationToken);
        return fileName;
    }

    public void Delete(string? fileName)
    {
        // Externally hosted media (absolute URLs stored by CatalogSeeder) have no local file.
        if (string.IsNullOrWhiteSpace(fileName) || Store.Application.Common.LocalMediaUrlBuilder.IsAbsoluteUrl(fileName))
        {
            return;
        }

        // The name is always server-generated (GUID + extension), but guard against traversal anyway.
        var filePath = Path.GetFullPath(Path.Combine(_rootPath, fileName));
        if (filePath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase) && File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    public string? GetUrl(string? fileName) =>
        string.IsNullOrWhiteSpace(fileName) ? null
        : Store.Application.Common.LocalMediaUrlBuilder.IsAbsoluteUrl(fileName) ? fileName
        : $"{RequestPath}/{fileName}";
}
