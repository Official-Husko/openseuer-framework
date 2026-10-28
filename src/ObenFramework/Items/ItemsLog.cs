using System;

namespace ObenFramework.Items;

/// <summary>Logging for the item and recipe code, safe to call while the framework is not loaded.</summary>
internal static class ItemsLog
{
    public static void Warn(string message) => FrameworkMod.Instance?.Log.Warn(message);

    public static void Error(string message, Exception e = null)
    {
        var log = FrameworkMod.Instance?.Log;
        if (log == null)
            return;
        if (e == null)
            log.Error(message);
        else
            log.Error(message, e);
    }
}
