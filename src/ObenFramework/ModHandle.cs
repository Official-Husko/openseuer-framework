// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenLoader;

namespace ObenFramework;

/// <summary>
/// The framework's per-mod services as calls on the mod itself, like the reference framework's
/// <c>CoreMod</c> handle: <c>this.SaveData&lt;T&gt;()</c>, <c>this.Postfix(...)</c>. Each is the same as the
/// static API with the mod as owner, so everything goes away when the mod unloads. Inside the mod class,
/// write <c>this.</c> in front (C# needs it for extension calls). Hotkeys and settings pages are on
/// ObenUIKit's <c>KitHandle</c>.
/// </summary>
public static class ModHandle
{
    /// <summary>The mod's data for the current save: <see cref="ModSaves.Register{T}"/>.</summary>
    public static ModSaveData<T> SaveData<T>(this ObenMod mod, string key = null) where T : class, new() =>
        ModSaves.Register<T>(mod, key);

    /// <summary>What the mod stored in the loaded save, read without registering: <see cref="ModSaves.Peek{T}"/>.</summary>
    public static T PeekSaveData<T>(this ObenMod mod, string key = null) where T : class =>
        ModSaves.Peek<T>(mod.Info.Id, key);

    /// <summary>A prefix on a method found by name (and <paramref name="args"/> when the name isn't unique): <see cref="ModPatches.Prefix"/>.</summary>
    public static bool Prefix(this ObenMod mod, Type type, string method, Type handlerType, string handlerName, Type[] args = null, string area = null) =>
        ModPatches.Prefix(mod, type, method, args, handlerType, handlerName, area);

    /// <summary>A postfix: <see cref="ModPatches.Postfix"/>.</summary>
    public static bool Postfix(this ObenMod mod, Type type, string method, Type handlerType, string handlerName, Type[] args = null, string area = null) =>
        ModPatches.Postfix(mod, type, method, args, handlerType, handlerName, area);

    /// <summary>A finalizer: <see cref="ModPatches.Finalizer"/>.</summary>
    public static bool Finalizer(this ObenMod mod, Type type, string method, Type handlerType, string handlerName, Type[] args = null, string area = null) =>
        ModPatches.Finalizer(mod, type, method, args, handlerType, handlerName, area);

    /// <summary>A transpiler: <see cref="ModPatches.Transpiler"/>.</summary>
    public static bool Transpiler(this ObenMod mod, Type type, string method, Type handlerType, string handlerName, Type[] args = null, string area = null) =>
        ModPatches.Transpiler(mod, type, method, args, handlerType, handlerName, area);

    /// <summary>The mod's patch area is in place (none of its patches failed): <see cref="PatchAreas.IsAvailable(ObenMod, string)"/>.</summary>
    public static bool IsPatched(this ObenMod mod, string area) => PatchAreas.IsAvailable(mod, area);
}
