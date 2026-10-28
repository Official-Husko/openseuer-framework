// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Story;
using PixelCrushers.DialogueSystem;

namespace ObenFramework.Dialogue;

/// <summary>
/// Lines whose text comes from a callback (<see cref="ConversationLine.TextCallback"/>). The entry's text is
/// set to a <c>[lua(Oben_LineText("id"))]</c> tag, which the Dialogue System replaces each time the line shows.
/// </summary>
internal static class LineTexts
{
    private sealed class Entry
    {
        public string Title;
        public Func<string> Callback;
        public string Fallback;
    }

    private static readonly Dictionary<string, Entry> Texts = new Dictionary<string, Entry>(StringComparer.Ordinal);

    public static void Prepare(string title, Conversation conversation, ConversationDefinition definition)
    {
        for (int i = 0; i < definition.Lines.Count; i++)
        {
            var line = definition.Lines[i];
            if (line.TextCallback == null)
                continue;
            var entry = conversation.GetDialogueEntry(i + 1);
            string id = DialoguePlan.HashedKey(title + "\n" + line.Key);
            Texts[id] = new Entry { Title = title, Callback = line.TextCallback, Fallback = line.Text };
            entry.DialogueText = "[lua(Oben_LineText(\"" + id + "\"))]";
        }
    }

    public static void Drop(string title)
    {
        var stale = new List<string>();
        foreach (var pair in Texts)
        {
            if (pair.Value.Title == title)
                stale.Add(pair.Key);
        }
        foreach (string id in stale)
            Texts.Remove(id);
    }

    public static void Clear() => Texts.Clear();

    /// <summary>The text for a line id; the fallback text when the callback returns null or text that breaks the rules.</summary>
    public static string Text(string id)
    {
        if (id == null || !Texts.TryGetValue(id, out var entry))
            return "";
        try
        {
            string text = entry.Callback();
            if (text == null)
                return entry.Fallback ?? "";
            if (DialoguePlan.ValidText(text, DialoguePlan.MaxText, false))
                return text;
            StoryLog.Refused("Line text of " + entry.Title, "the callback returned text that is too long or has control characters");
            return entry.Fallback ?? "";
        }
        catch (Exception e)
        {
            StoryLog.Failed("Line text of " + entry.Title, e);
            return entry.Fallback ?? "";
        }
    }
}
