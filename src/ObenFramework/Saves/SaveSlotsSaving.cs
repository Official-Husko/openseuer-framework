// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections;
using ObenFramework.Events;
using ObenLoader;
using UnityEngine;

namespace ObenFramework.Saves;

public static partial class SaveSlots
{
    // How long a requested save may stay "pending" without the game reporting it written.
    private const float PendingSeconds = 10f;

    private static float _pendingUntil;

    internal static void Init(ObenMod framework)
    {
        _pendingUntil = 0f;
        GameEvents.Saved.Subscribe(framework, _ => _pendingUntil = 0f);
    }

    /// <summary>
    /// A save can be started now: a world is loaded, nothing is saving, loading, fading, sleeping,
    /// in a conversation or being arrested, and the pause menu's save button is enabled (the game
    /// turns it off on difficulties without manual saves and in a few scripted moments).
    /// </summary>
    public static bool CanSaveNow
    {
        get
        {
            try { return SaveAllowed(); }
            catch (Exception) { return false; }
        }
    }

    /// <summary>
    /// Saves through the game's own save path, the way its autosave does. With no slot it writes
    /// the next autosave in the game's rotation (<c>Autosave</c> or <c>Autosave2</c>); with a slot
    /// name from <see cref="SlotNames"/> it writes that slot, replacing what was in it. The save
    /// starts a couple of frames later so the game can show its saving screen first; listen to
    /// <see cref="GameEvents.Saved"/> for the result. Returns false when it could not start.
    /// </summary>
    public static bool SaveNow(string slot = null)
    {
        try
        {
            if (!CanSaveNow)
                return false;
            if (slot == null)
            {
                SaveController.Autosave(SaveController.instance);
            }
            else
            {
                var name = Canonical(slot);
                if (name == null)
                    return false;
                SaveController.instance.StartCoroutine(SaveSlotDelayed(name));
            }
            _pendingUntil = Time.realtimeSinceStartup + PendingSeconds;
            return true;
        }
        catch (Exception e)
        {
            SaveState.Log.Error("Starting a save failed", e);
            return false;
        }
    }

    private static string Canonical(string slot)
    {
        foreach (var name in SlotNames)
        {
            if (string.Equals(name, slot, StringComparison.OrdinalIgnoreCase))
                return name;
        }
        return null;
    }

    // Same sequence as the game's autosave: saving screen up, two frames, save, hide.
    private static IEnumerator SaveSlotDelayed(string slot)
    {
        SavingUI.instance.ShowSavingUI(fullScreen: true);
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();
        SaveController.SaveGame(slot);
        yield return new WaitForEndOfFrame();
        yield return null;
        SavingUI.instance.HideSavingUI();
    }

    private static bool SaveAllowed()
    {
        var controller = SaveController.instance;
        if (controller == null || !controller.gameObject.activeInHierarchy)
            return false;
        var folder = CurrentFolder;
        if (string.IsNullOrEmpty(folder) || !System.IO.Directory.Exists(folder))
            return false;
        if (PlayerIdentity.identity == null || GameController.instance == null || SavingUI.instance == null)
            return false;
        if (PauseMenu.instance == null || DestructibleList.instance == null)
            return false;
        if (SaveGate.IsBusy || Time.realtimeSinceStartup < _pendingUntil || WorldBusy())
            return false;
        // The pause menu stops time for itself; any other stopped time is a moment the game does not save in.
        if (Time.timeScale == 0f && !PauseMenu.instance.PauseIsActive())
            return false;
        return SaveButtonEnabled();
    }

    private static bool WorldBusy()
    {
        var screen = LoadingScreen.instance;
        if (screen != null && (screen.Fading || !screen.LevelLoadDone))
            return true;
        if (Death.instance != null && Death.instance.dead)
            return true;
        if (WaitingController.instance != null && WaitingController.instance.waitingType != WaitingController.Type.None)
            return true;
        if (Changelevel.changeLevelDelayActive)
            return true;
        if (Crime.instance != null && Crime.instance.ArrestInProgress)
            return true;
        if (SleepEventController.instance != null && SleepEventController.instance.eventInProgress)
            return true;
        return DialogueController.instance != null && DialogueController.instance.DialogueActive;
    }

    private static bool SaveButtonEnabled()
    {
        var button = PauseMenu.instance.saveMenuButton;
        if (button == null)
            return false;
        var ui = button.GetComponent<UnityEngine.UI.Button>();
        return ui != null && ui.interactable;
    }
}
