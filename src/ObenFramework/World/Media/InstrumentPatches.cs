// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using UnityEngine;
using WieldableItem;

namespace ObenFramework.World;

/// <summary>Strums, NPC comments and mod-held notes on the guitar.</summary>
internal static class InstrumentPatches
{
    private const string Positive = "Generic/Guitar/Guitar Positive";
    private const string Negative = "Generic/Guitar/Guitar Negative";

    [HarmonyPatch(typeof(WieldableGuitar), nameof(WieldableGuitar.onPrimaryUse))]
    private static class StrumPatch
    {
        [HarmonyPrefix]
        private static void Before(WieldableGuitar __instance, out bool __state) =>
            __state = Instruments.Strummed.HasSubscribers && Instruments.CanPlay(__instance);

        [HarmonyPostfix]
        private static void After(WieldableGuitar __instance, bool __state)
        {
            try
            {
                if (__state && WorldChanges.Active)
                    Instruments.Strummed.Raise(__instance);
            }
            catch (Exception e) { WorldLog.Failed("Guitar strum hook", e); }
        }
    }

    // The guitar stops a long note as soon as the player lets go of the key; a note a mod holds has no key.
    [HarmonyPatch(typeof(WieldableGuitar), "Update")]
    private static class HoldPatch
    {
        [HarmonyPrefix]
        private static bool Before(WieldableGuitar __instance) => Instruments.Holding != __instance;
    }

    [HarmonyPatch(typeof(NPCRandomTalk), nameof(NPCRandomTalk.OnGuitarPlayed))]
    private static class NpcCommentPatch
    {
        [HarmonyPostfix]
        private static void After(NPCRandomTalk __instance, bool positive) => Raise(__instance, positive);
    }

    // Bystanders without an NPCRandomTalk get the comment conversation straight from the guitar.
    [HarmonyPatch(typeof(CharacterBarkStarter), nameof(CharacterBarkStarter.TryToComment))]
    private static class BystanderCommentPatch
    {
        [HarmonyPostfix]
        private static void After(CharacterBarkStarter __instance, string _conversation)
        {
            if (_conversation != Positive && _conversation != Negative)
                return;
            if (__instance.GetComponent<NPCRandomTalk>() == null)
                Raise(__instance, _conversation == Positive);
        }
    }

    private static void Raise(Component witness, bool positive)
    {
        try
        {
            if (!Instruments.Commented.HasSubscribers || !WorldChanges.Active)
                return;
            var npc = witness.GetComponentInParent<NPCController>();
            Instruments.Commented.Raise(new GuitarComment(witness, npc != null ? npc.ID : 0, positive));
        }
        catch (Exception e) { WorldLog.Failed("Guitar comment hook", e); }
    }
}
