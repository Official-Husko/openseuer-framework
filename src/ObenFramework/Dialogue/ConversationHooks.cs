using System;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace ObenFramework.Dialogue;

/// <summary>
/// Forwards the Dialogue System's own conversationStarted / conversationEnded events as
/// <see cref="Conversations.Started"/> and <see cref="Conversations.Ended"/>. Those fire for every
/// conversation (NPC talk, phone call, narration), exactly once each, unlike the game's
/// DialogueController.onDialogueClosed which is a single callback slot that is never cleared.
/// </summary>
internal static class ConversationHooks
{
    private static readonly TransformDelegate StartedHandler = OnStarted;
    private static readonly TransformDelegate EndedHandler = OnEnded;

    private static DialogueSystemController _attached;

    public static void Attach(ObenMod framework)
    {
        // The controller is persistent, but a scene prefab can replace it (DeleteOtherDialogueManagers),
        // so look again whenever a scene comes up.
        GameEvents.MainMenu.Subscribe(framework, Refresh);
        GameEvents.SceneLoaded.Subscribe(framework, _ => Refresh());
        GameEvents.WorldReady.Subscribe(framework, _ => Refresh());
        framework.Track(Disposable.Create(Detach));
        Refresh();
    }

    private static void Refresh()
    {
        var current = DialogueManager.hasInstance ? DialogueManager.instance : null;
        if (current == _attached)
            return;

        Detach();
        if (current == null)
            return;
        current.conversationStarted += StartedHandler;
        current.conversationEnded += EndedHandler;
        _attached = current;
    }

    private static void Detach()
    {
        if (_attached != null)
        {
            _attached.conversationStarted -= StartedHandler;
            _attached.conversationEnded -= EndedHandler;
        }
        _attached = null;
    }

    private static void OnStarted(Transform _)
    {
        try
        {
            Conversations.Started.Raise(Describe(DialogueManager.lastConversationStarted), GameEvents.Log);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Raising Conversations.Started failed", e);
        }
    }

    private static void OnEnded(Transform _)
    {
        try
        {
            Conversations.Ended.Raise(Describe(DialogueManager.lastConversationEnded), GameEvents.Log);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Raising Conversations.Ended failed", e);
        }
    }

    // The controller forgets the speakers after it raised the end event, so both are still valid here.
    private static ConversationInfo Describe(string title) =>
        new ConversationInfo(title, DialogueManager.currentActor, DialogueManager.currentConversant);
}
