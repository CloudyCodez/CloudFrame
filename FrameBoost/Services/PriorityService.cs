using System.ComponentModel;
using System.Diagnostics;

namespace FrameBoost.Services;

internal sealed class PriorityService
{
    public bool TryGetPriority(Process process, out ProcessPriorityClass priorityClass)
    {
        try
        {
            priorityClass = process.PriorityClass;
            return true;
        }
        catch (Win32Exception)
        {
        }
        catch (InvalidOperationException)
        {
        }

        priorityClass = ProcessPriorityClass.Normal;
        return false;
    }

    public bool TrySetPriority(Process process, ProcessPriorityClass priorityClass, out string? error)
    {
        try
        {
            process.PriorityClass = priorityClass;
            error = null;
            return true;
        }
        catch (Win32Exception ex)
        {
            error = ex.Message;
            return false;
        }
        catch (InvalidOperationException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
