using HarmonyLib;
using ObenFramework.Features;
using PixelCrushers.DialogueSystem;

namespace ObenPatches.Fixes;

/// <summary>
/// Two Lua functions the dialogue calls are registered against a method that doesn't exist, so they
/// are registered as empty and always answer nil. Bank.RegisterDialogue looks up a method called
/// CheckIfPlayerHasAccount, but the method is PlayerHasAnAccount; the pocket bank conversation
/// (Penni-Pena) calls it to fill PocketBankHasAccount, so it always thinks the player has no
/// account. RelationshipController.Register looks up RelationshipIdActive on
/// PlayerSubscriptionsController instead of on itself. The fix registers both against the right
/// methods right after the game registers them.
/// </summary>
internal sealed class DialogueLuaFunctions : ModFeature
{
    public const string Key = "DialogueLuaFunctions";

    public override string Id => Key;
    public override string Name => "Bank and relationship checks in dialogue work";
    public override string Description => "The pocket bank conversation can tell if you already have an account.";
}

[HarmonyPatchCategory(DialogueLuaFunctions.Key)]
[HarmonyPatch(typeof(Bank), nameof(Bank.RegisterDialogue))]
internal static class BankAccountCheckPatch
{
    // Only act when this call does the registering; later calls do nothing in the game either.
    private static void Prefix(bool ___dialogueRegistered, out bool __state) => __state = !___dialogueRegistered;

    private static void Postfix(Bank __instance, bool __state)
    {
        if (__state)
            Lua.environment.RegisterMethodFunction("CheckIfPlayerHasAccount", __instance,
                AccessTools.Method(typeof(Bank), nameof(Bank.PlayerHasAnAccount)));
    }
}

[HarmonyPatchCategory(DialogueLuaFunctions.Key)]
[HarmonyPatch(typeof(RelationshipController), nameof(RelationshipController.Register))]
internal static class RelationshipIdActivePatch
{
    private static void Prefix(bool ___registered, out bool __state) => __state = !___registered;

    private static void Postfix(RelationshipController __instance, bool __state)
    {
        if (__state)
            Lua.environment.RegisterMethodFunction("RelationshipIdActive", __instance,
                AccessTools.Method(typeof(RelationshipController), nameof(RelationshipController.RelationshipIdActive)));
    }
}
