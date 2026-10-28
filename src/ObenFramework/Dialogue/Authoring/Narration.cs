// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Game;
using ObenFramework.Saves;
using ObenFramework.Story;
using ObenLoader;

namespace ObenFramework.Dialogue;

/// <summary>
/// Text over a black screen, the way the game narrates waking up or passing out: the screen fades to black,
/// the text plays as a conversation with no speaker, and the screen fades back when the player closes it.
/// </summary>
public static class Narration
{
    internal const string Key = "narration";

    /// <summary>
    /// Shows <paramref name="text"/> (up to 1000 characters). False when no world is ready, a conversation or
    /// the game's sleep/arrest sequence is running, or a save runs.
    /// </summary>
    public static bool Show(ObenMod owner, string text)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (!DialoguePlan.ValidText(text, DialoguePlan.MaxText, true) || !WorldState.IsReady || Conversations.IsActive
            || SaveGate.IsBusy || BlackCanvas.instance == null || BlackCanvas.instance.interactableTalk == null || !Clear())
            return false;

        var canvas = BlackCanvas.instance;
        var talk = canvas.interactableTalk;
        Action previous = talk.onEndTalkCallback;
        bool started = false;
        try
        {
            DialogueAuthoring.RemoveTitle(owner, owner.Info.Id + "." + Key);
            var definition = new ConversationDefinition
            {
                Key = Key,
                Lines = new List<ConversationLine> { new ConversationLine { Key = "text", Text = text } },
            };
            var added = DialogueAuthoring.AddWith(owner, definition, -1);
            if (!added.Added)
                return false;
            talk.onEndTalkCallback = () =>
            {
                FadeBack();
                previous?.Invoke();
            };
            started = true;
            canvas.StartDialogue(added.Key);
        }
        catch (Exception e)
        {
            StoryLog.Failed("Narration.Show", e);
        }
        if (Conversations.IsActive)
            return true;
        talk.onEndTalkCallback = previous;
        if (started)
            FadeBack();
        return false;
    }

    // The game's own waiting sequences (sleep, pass out, arrest) use the black screen too.
    private static bool Clear()
    {
        var waiting = WaitingController.instance;
        return waiting == null || (waiting.waitingType == WaitingController.Type.None && string.IsNullOrEmpty(waiting.eventDialogue));
    }

    private static void FadeBack()
    {
        try
        {
            if (BlackCanvas.instance != null)
                BlackCanvas.instance.fadeCanvasGroup.StartFadeOut(0.005f);
        }
        catch (Exception e)
        {
            StoryLog.Failed("Narration fade", e);
        }
    }
}
