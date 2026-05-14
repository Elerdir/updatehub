using System.Security.Cryptography;
using UpdateHub.Application.Interfaces;

namespace UpdateHub.Infrastructure.Storage;

public class LocalArtifactStorage : IArtifactStorage
{
    private readonly string _storagePath;

    public LocalArtifactStorage(string storagePath)
    {
        _storagePath = storagePath;
        Directory.CreateDirectory(_storagePath);
    }

    public async Task<(string storedPath, string sha256, long fileSize)> StoreAsync(
        Stream stream, string fileName, string appSlug, string version)
    {
        var dir = Path.Combine(_storagePath, appSlug, version);
        Directory.CreateDirectory(dir);

        var safeFileName = Path.GetFileName(fileName);
        var storedPath   = Path.Combine(dir, safeFileName);

        // Stream straight to disk, hashing as we go — never buffers the whole
        // file in memory (installers can be hundreds of MB).
        using var sha = SHA256.Create();
        long fileSize;
        try
        {
            await using var fileStream = new FileStream(
                storedPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 81920, useAsync: true);
            await using var cryptoStream = new CryptoStream(
                fileStream, sha, CryptoStreamMode.Write);

            await stream.CopyToAsync(cryptoStream);
            await cryptoStream.FlushFinalBlockAsync();
            fileSize = fileStream.Length;
        }
        catch
        {
            // Don't leave a partial file behind on failure.
            if (File.Exists(storedPath)) File.Delete(storedPath);
            throw;
        }

        var hash = Convert.ToHexString(sha.Hash!).ToLower();
        return (storedPath, hash, fileSize);
    }

    public void Delete(string storedPath)
    {
        if (File.Exists(storedPath))
            File.Delete(storedPath);
    }

    public Stream OpenRead(string storedPath) => File.OpenRead(storedPath);
}
