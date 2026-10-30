using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Dialogue;
using PixelCrushers.DialogueSystem;

namespace DevBridge.Commands;

/// <summary>Talking from the bridge: start a conversation, read what shows, continue a line, pick a choice.</summary>
internal static class ConversationCommands
{
    [BridgeCommand("dialogue.start", "Talk to an NPC in the scene like pressing Use, or open a conversation without an NPC.",
        Params = new[] { "npc?: name on the Talk prompt or full name", "conversation?: title (required without npc)", "force?: stop a running conversation first, default false" })]
    private static object Start(JObject p)
    {
        string npc = p.Str("npc", null);
        string conversation = p.Str("conversation", null);
        var result = npc != null
            ? Conversations.Start(npc, conversation, p.Bool("force", false))
            : Conversations.StartRaw(conversation ?? throw new ArgumentException("Give an npc or a conversation."));
        return new JObject { ["result"] = result.ToString(), ["title"] = Conversations.CurrentTitle };
    }

    [BridgeCommand("dialogue.state", "What the running conversation shows: the line, its speaker, whether it waits for continue, and the player's choices.")]
    private static object State(JObject p)
    {
        var state = new JObject { ["active"] = Conversations.IsActive, ["title"] = Conversations.CurrentTitle };
        var controller = Controller();
        var current = controller?.currentState;
        if (current == null)
            return state;
        var subtitle = current.subtitle;
        state["entryId"] = subtitle?.dialogueEntry?.id;
        state["speaker"] = subtitle?.speakerInfo?.Name;
        state["text"] = subtitle?.formattedText?.text;
        state["waitingForContinue"] = controller.conversationView?.isWaitingForContinue ?? false;
        var choices = new JArray();
        if (current.hasPCResponses)
        {
            foreach (var response in current.pcResponses)
                choices.Add(new JObject { ["text"] = response.formattedText?.text, ["enabled"] = response.enabled, ["entryId"] = response.destinationEntry?.id });
        }
        state["choices"] = choices;
        return state;
    }

    [BridgeCommand("dialogue.continue", "Go on from the line that is showing, like the continue key.")]
    private static object Continue(JObject p)
    {
        var view = Controller()?.conversationView;
        if (view == null || !view.isWaitingForContinue)
            return new JObject { ["continued"] = false };
        view.HandleContinueButtonClick();
        return new JObject { ["continued"] = true };
    }

    [BridgeCommand("dialogue.pick", "Pick one of the player's choices that are showing.",
        Params = new[] { "text?: part of the choice's text (case-insensitive)", "index?: its place in dialogue.state's choices, from 0" })]
    private static object Pick(JObject p)
    {
        var controller = Controller();
        var current = controller?.currentState;
        if (current == null || !current.hasPCResponses)
            throw new InvalidOperationException("No choices are showing.");
        var responses = current.pcResponses;
        Response picked = null;
        int index = p.Int("index", -1);
        string text = p.Str("text", null);
        if (index >= 0 && index < responses.Length)
            picked = responses[index];
        else if (text != null)
            picked = Array.Find(responses, r => r.enabled && (r.formattedText?.text ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
        if (picked == null)
            throw new ArgumentException("No such choice; give text or index from dialogue.state.");
        controller.conversationView.SelectResponse(new SelectedResponseEventArgs(picked));
        return new JObject { ["picked"] = picked.formattedText?.text };
    }

    [BridgeCommand("dialogue.stop", "End the running conversation.")]
    private static object Stop(JObject p)
    {
        bool was = Conversations.IsActive;
        Conversations.Stop();
        return new JObject { ["stopped"] = was };
    }

    private static ConversationController Controller() => DialogueManager.hasInstance ? DialogueManager.conversationController : null;
}
