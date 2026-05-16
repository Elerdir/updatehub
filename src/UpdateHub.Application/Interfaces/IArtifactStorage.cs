namespace UpdateHub.Application.Interfaces;

public interface IArtifactStorage
{
    Task<(string storedPath, string sha256, long fileSize)> StoreAsync(
        Stream stream, string fileName, string appSlug, string version);
    void Delete(string storedPath);
    Stream OpenRead(string storedPath);

    /// <summary>Total bytes consumed by the storage root (sum of all artifact files).</summary>
    long GetTotalBytes();
}
