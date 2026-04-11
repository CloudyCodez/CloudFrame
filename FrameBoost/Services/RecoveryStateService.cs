using System.Text.Json;
using FrameBoost.Core;
using FrameBoost.Models;

namespace FrameBoost.Services;

internal sealed class RecoveryStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public BoostRecoveryState? Load()
    {
        AppPaths.EnsureDataDirectory();

        if (!File.Exists(AppPaths.RecoveryPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(AppPaths.RecoveryPath);
            return JsonSerializer.Deserialize<BoostRecoveryState>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save(BoostRecoveryState state)
    {
        AppPaths.EnsureDataDirectory();
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(AppPaths.RecoveryPath, json);
    }

    public void Delete()
    {
        if (File.Exists(AppPaths.RecoveryPath))
        {
            File.Delete(AppPaths.RecoveryPath);
        }
    }
}
