using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ObenFramework.Building;

/// <summary>
/// The game reads saves with its own fast reader (<c>FsStreamReader</c>) and only falls back to FullSerializer
/// when that fails. The fast reader decides by itself which saved objects shrink to their asset path, without
/// <c>SavedAssets.IsCollapsible</c>, so a custom piece (saved in full under its template's path) would load as the
/// template. This keeps those whole there too; they then go through the game's asset converter and are linked
/// back by id like on the slow path.
/// </summary>
internal static class FurnitureSaveReader
{
    private static readonly Type Reader = typeof(SavedAssets).Assembly.GetType("FsStreamReader");

    private static AccessTools.FieldRef<object, List<int>> _keys;
    private static Func<object, int, string> _text;

    [HarmonyPatch]
    private static class KeepInFull
    {
        [HarmonyPrepare]
        private static bool Prepare() => Reader != null;

        [HarmonyTargetMethod]
        private static MethodBase Target() => AccessTools.Method(Reader, "IsCollapsible");

        [HarmonyPostfix]
        private static void After(object __instance, int keyBase, ref string path, ref bool __result)
        {
            if (!__result)
                return;
            try
            {
                if (!(SavedAssets.GetAsset(path) is global::Furniture))
                    return;
                string id = IdOf(__instance, keyBase);
                if (id == null || FurnitureIds.Classify(id) != FurnitureIdKind.Furniture)
                    return;
                path = null;
                __result = false;
            }
            catch (Exception e)
            {
                FurnitureRegistry.Log.Error("Fast save reader hook", e);
            }
        }
    }

    // The object being read has its keys from keyBase on; each key's value is the node right after it.
    private static string IdOf(object reader, int keyBase)
    {
        _keys ??= AccessTools.FieldRefAccess<List<int>>(Reader, "keys");
        _text ??= TextOf();
        var keys = _keys(reader);
        for (int i = keyBase; i < keys.Count; i++)
        {
            if (_text(reader, keys[i]) == "id")
                return _text(reader, keys[i] + 1);
        }
        return null;
    }

    // The reader is internal, so Harmony can't bind its GetString to an object-typed delegate; cast in IL.
    private static Func<object, int, string> TextOf()
    {
        var method = AccessTools.Method(Reader, "GetString", new[] { typeof(int) });
        var dynamic = new DynamicMethod("FsStreamReader_GetString", typeof(string), new[] { typeof(object), typeof(int) }, Reader, true);
        var il = dynamic.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, Reader);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Call, method);
        il.Emit(OpCodes.Ret);
        return (Func<object, int, string>)dynamic.CreateDelegate(typeof(Func<object, int, string>));
    }
}
