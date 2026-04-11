namespace FrameBoost.Services;

internal sealed class ShaderCacheService
{
    public string CacheDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "D3DSCache");

    public long GetCacheSizeBytes()
    {
        if (!Directory.Exists(CacheDirectory))
        {
            return 0;
        }

        return Directory.EnumerateFiles(CacheDirectory, "*", SearchOption.AllDirectories)
            .Select(static path => new FileInfo(path))
            .Sum(static info => info.Exists ? info.Length : 0L);
    }

    public int ClearCache()
    {
        if (!Directory.Exists(CacheDirectory))
        {
            return 0;
        }

        var deletedFiles = 0;

        foreach (var file in Directory.EnumerateFiles(CacheDirectory, "*", SearchOption.AllDirectories))
        {
            try
            {
                File.Delete(file);
                deletedFiles++;
            }
            catch
            {
            }
        }

        return deletedFiles;
    }
}
