// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;

namespace ObenFramework.Player;

/// <summary>
/// Raises <see cref="PlayerNotes.Collected"/> from <c>NoteController.AddNote</c> (every pickup, quest
/// reward and dialogue gift goes through it) and <see cref="PlayerNotes.Read"/> from the notes page
/// selecting an unread entry.
/// </summary>
[HarmonyPatch]
internal static class NotePatches
{
    internal struct Adding
    {
        public bool Watch;
        public bool HadBook;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NoteController), nameof(NoteController.AddNote))]
    private static void BeforeAdd(NoteController __instance, NoteInfo note, out Adding __state)
    {
        __state = default;
        try
        {
            if (note == null)
                return;
            __state.Watch = !PlayerNotes.Holds(__instance, note);
            __state.HadBook = note.book != null && PlayerNotes.FindBook(__instance, note.book) != null;
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Checking a note before it was added failed", e);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NoteController), nameof(NoteController.AddNote))]
    private static void AfterAdd(NoteController __instance, NoteInfo note, Adding __state)
    {
        if (!__state.Watch)
            return;
        try
        {
            if (PlayerNotes.Holds(__instance, note))
                PlayerNotes.Collected.Raise(new NoteCollected(note, note.book != null && !__state.HadBook), GameEvents.Log);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Reporting a collected note failed", e);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NoteUIPanel), nameof(NoteUIPanel.Selected))]
    private static void BeforeSelected(NoteUIPanel __instance, out bool __state) =>
        __state = __instance.note != null && !__instance.note.read;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NoteUIPanel), nameof(NoteUIPanel.Selected))]
    private static void AfterSelected(NoteUIPanel __instance, bool __state)
    {
        if (!__state || __instance.note == null || !__instance.note.read)
            return;
        try
        {
            PlayerNotes.Read.Raise(new NoteEntry(__instance.note), GameEvents.Log);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Reporting a read note failed", e);
        }
    }
}
