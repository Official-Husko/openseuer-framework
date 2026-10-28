using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenLoader.Mods;

namespace DevBridge.Commands;

/// <summary>
/// Co-op state for tools. ObenCoop builds the JSON itself (CoopMod.BridgeStatus and friends); these
/// commands only find the loaded mod and call it by name, so the bridge has no reference to ObenCoop
/// and either mod can be reloaded or missing on its own.
/// </summary>
internal static class CoopCommands
{
    [BridgeCommand("coop.status", "Co-op session, roster with ping, traffic, lobby (kind, address, join code) and Steam.")]
    private static object Status(JObject p) => Call("BridgeStatus");

    [BridgeCommand("coop.presence", "Co-op presence: what this game shares and every other player's area and pose right now.")]
    private static object Presence(JObject p) => Call("BridgePresence");

    [BridgeCommand("coop.lobbies", "The last co-op lobby list (coop lobby list in the console starts one), with verdicts.")]
    private static object Lobbies(JObject p) => Call("BridgeLobbies");

    private static object Call(string method)
    {
        var mod = ModRegistry.Find("ObenCoop");
        var instance = mod != null && mod.State == ModState.Loaded ? mod.Instance : null;
        if (instance == null)
            throw new InvalidOperationException("ObenCoop isn't loaded.");
        var target = instance.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance);
        if (target == null)
            throw new InvalidOperationException($"This ObenCoop has no {method}; it is older than the bridge.");
        try
        {
            return target.Invoke(instance, null);
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }
}
