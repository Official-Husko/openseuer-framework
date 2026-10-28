// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Globalization;
using UnityEngine;

namespace DevBridge.Tracing;

/// <summary>
/// Short text for Unity values in trace records. A Unity object's name may only be read on the main thread,
/// so other threads get its type alone.
/// </summary>
internal static class UnityValues
{
    public static string Describe(object value)
    {
        switch (value)
        {
            case Object unity:
                if (!TraceHooks.IsMainThread)
                    return unity.GetType().Name;
                return unity == null ? unity.GetType().Name + " (destroyed)" : unity.GetType().Name + " \"" + TraceFormat.Cut(unity.name) + "\"";
            case Vector3 v:
                return string.Format(CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}, {2:0.##})", v.x, v.y, v.z);
            case Vector2 v:
                return string.Format(CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##})", v.x, v.y);
            case Quaternion q:
                return TraceHooks.IsMainThread
                    ? "euler" + Describe(q.eulerAngles)
                    : string.Format(CultureInfo.InvariantCulture, "quat({0:0.###}, {1:0.###}, {2:0.###}, {3:0.###})", q.x, q.y, q.z, q.w);
            case Color c:
                return string.Format(CultureInfo.InvariantCulture, "rgba({0:0.##}, {1:0.##}, {2:0.##}, {3:0.##})", c.r, c.g, c.b, c.a);
            default:
                return null;
        }
    }
}
