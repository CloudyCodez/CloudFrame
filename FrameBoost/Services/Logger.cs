using FrameBoost.Core;

namespace FrameBoost.Services;

internal sealed class Logger
{
    private readonly Lock _lock = new();

    public event Action<string>? MessageLogged;

    public void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";

        lock (_lock)
        {
            AppPaths.EnsureDataDirectory();
            File.AppendAllLines(AppPaths.LogPath, [line]);
        }

        MessageLogged?.Invoke(line);
    }

    public void Clear()
    {
        lock (_lock)
        {
            AppPaths.EnsureDataDirectory();
            File.WriteAllText(AppPaths.LogPath, string.Empty);
        }
    }
}
