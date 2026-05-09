using System.Security.Cryptography;

namespace UpdateHub.Web.Services;

public class ArtifactStorageService
{
    private readonly string _storagePath;

    public ArtifactStorageService(IConfiguration config)
    {
        _storagePath = config["UpdateHub:StoragePath"]
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "artifacts");
        Directory.CreateDirectory(_storagePath);
    }

    public async Task<(string storedPath, string sha256, long fileSize)> StoreAsync(
        Stream stream, string fileName, string appSlug, string version)
    {
        var dir = Path.Combine(_storagePath, appSlug, version);
        Directory.CreateDirectory(dir);

        var safeFileName = Path.GetFileName(fileName);
        var storedPath = Path.Combine(dir, safeFileName);

        var buffer = new byte[81920];
        using var memStream = new MemoryStream();
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
            memStream.Write(buffer, 0, bytesRead);

        var bytes = memStream.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLower();

        await File.WriteAllBytesAsync(storedPath, bytes);
        return (storedPath, hash, bytes.Length);
    }

    public void Delete(string storedPath)
    {
        if (File.Exists(storedPath))
            File.Delete(storedPath);
    }

    public Stream OpenRead(string storedPath) => File.OpenRead(storedPath);
}
