// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Text;
using ObenFramework.Quests;

namespace ObenFramework.Dialogue;

/// <summary>
/// The rules for conversations, responses and branches mods add, the ids they get, and how an anchor line
/// is matched by its text. Priorities are the Dialogue System's ConditionPriority numbers (Low 0 to High 4).
/// No Unity types, so it is unit tested on its own.
/// </summary>
internal static class DialoguePlan
{
    public const int MaxLines = 200;
    public const int MaxText = 1000;
    public const int MaxLua = 500;

    /// <summary>Mod conversations and entries get ids from here on; the game's are below 1000.</summary>
    public const int FirstId = 1000000;
    public const int IdRange = 1000000;

    public const int Normal = 2;
    public const int High = 4;

    /// <summary>Checks a conversation definition. Null when it is fine, else why not.</summary>
    public static string Check(string modId, ConversationDefinition definition)
    {
        if (!QuestIds.ValidModId(modId))
            return $"the mod id '{modId}' can't be part of a conversation title";
        if (definition == null)
            return "the definition is null";
        if (!QuestIds.ValidKey(definition.Key))
            return $"the key '{definition.Key}' needs 1 to {QuestIds.MaxKey} letters, digits, '_' or '-'";
        if (definition.Speaker != null && (definition.Speaker.Length == 0 || definition.Speaker.Length > 200))
            return "the speaker must be an actor name or null";
        var lines = definition.Lines;
        if (lines == null || lines.Count < 1 || lines.Count > MaxLines)
            return $"a conversation needs 1 to {MaxLines} lines";

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            if (line == null)
                return "a line is null";
            if (!QuestIds.ValidKey(line.Key))
                return $"the line key '{line.Key}' needs 1 to {QuestIds.MaxKey} letters, digits, '_' or '-'";
            if (!keys.Add(line.Key))
                return $"two lines use the key '{line.Key}'";
            if (!ValidText(line.Text, MaxText, line.TextCallback == null))
                return $"line '{line.Key}' needs a text of 1 to {MaxText} characters (or a TextCallback)";
            if (!ValidText(line.Condition, MaxLua, false) || !ValidText(line.Script, MaxLua, false))
                return $"line '{line.Key}' has a condition or script over {MaxLua} characters or with control characters";
        }
        foreach (var line in lines)
        {
            if (line.Next == null)
                continue;
            foreach (string next in line.Next)
            {
                if (next == null || !keys.Contains(next))
                    return $"line '{line.Key}' leads to '{next}', which is not a line of this conversation";
            }
        }
        return null;
    }

    /// <summary>Text with at most <paramref name="max"/> characters and no control characters other than newlines.</summary>
    public static bool ValidText(string text, int max, bool required)
    {
        if (text == null)
            return !required;
        if (text.Length > max || (required && text.Trim().Length == 0))
            return false;
        foreach (char c in text)
        {
            if (char.IsControl(c) && c != '\n' && c != '\r')
                return false;
        }
        return true;
    }

    /// <summary>FNV-1a over the text: stable between runs, so ids and keys stay the same for the same input.</summary>
    public static uint Hash(string text)
    {
        uint hash = 2166136261u;
        foreach (char c in text)
            hash = unchecked((hash ^ c) * 16777619u);
        return hash;
    }

    public static string HashedKey(string text) => Hash(text).ToString("x8");

    /// <summary>A stable id in the mod range for a title or key.</summary>
    public static int StableId(string text) => FirstId + (int)(Hash(text) % IdRange);

    /// <summary>The first id from <paramref name="id"/> on (wrapping inside the mod range) that isn't taken.</summary>
    public static int FreeIdFrom(int id, Func<int, bool> taken)
    {
        for (int tries = 0; tries < IdRange && taken(id); tries++)
            id = id + 1 >= FirstId + IdRange ? FirstId : id + 1;
        return id;
    }

    /// <summary>The key a response gets: the same mod adding the same response twice gets the same key.</summary>
    public static string ResponseKey(string modId, string conversation, string anchorText, int anchorId, string playerText, string npcReply, string condition)
    {
        string anchor = anchorText != null ? "text\n" + anchorText : "id\n" + anchorId;
        return modId + "." + HashedKey(conversation + "\n" + anchor + "\n" + playerText + "\n" + (npcReply ?? "") + "\n" + (condition ?? ""));
    }

    public static string BranchKey(string modId, string conversation, string anchorText, int anchorId, string branchKey)
    {
        string anchor = anchorText != null ? "text\n" + anchorText : "id\n" + anchorId;
        return modId + "." + HashedKey(conversation + "\n" + anchor + "\n" + branchKey);
    }

    /// <summary>Where a new link goes in a line's list: the asked index, or the end when it is -1 or past the end.</summary>
    public static int InsertIndex(int requested, int count) => requested < 0 || requested > count ? count : requested;

    public static bool ValidPriority(int priority) => priority >= -1 && priority <= High;

    /// <summary>The asked priority, or <paramref name="fallback"/> (the anchor's own player choices) for -1.</summary>
    public static int RequestedPriority(int priority, int fallback) => priority < 0 ? fallback : priority;

    /// <summary>
    /// The priority of a branch's link. A branch that starts with the NPC talking would win over the player's
    /// menu at the same priority (the Dialogue System follows the highest NPC link), so when no priority was
    /// asked for it goes one below the menu's.
    /// </summary>
    public static int LinkPriority(int priority, int playerPriority, bool npcFirst)
    {
        int wanted = RequestedPriority(priority, playerPriority);
        if (priority >= 0 || !npcFirst || wanted == 0)
            return wanted;
        return wanted - 1;
    }

    /// <summary>
    /// The text of a line the way a modder would write it: the Dialogue System's typing codes (<c>\,</c>,
    /// <c>\.</c>, <c>\^</c>, <c>\&lt;</c>, <c>\&gt;</c>, <c>\|</c>) taken out, spaces collapsed, trimmed.
    /// The game's lines are full of <c>\,</c> pauses.
    /// </summary>
    public static string Plain(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        var sb = new StringBuilder(text.Length);
        bool space = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\\' && i + 1 < text.Length && ",.^<>|".IndexOf(text[i + 1]) >= 0)
            {
                i++;
                continue;
            }
            if (char.IsWhiteSpace(c))
            {
                space = sb.Length > 0;
                continue;
            }
            if (space)
                sb.Append(' ');
            space = false;
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// The lines whose text matches <paramref name="wanted"/>: exactly, else trimmed and ignoring case, else
    /// with the typing codes taken out (<see cref="Plain"/>). Returns the indexes of the first kind that matched.
    /// </summary>
    public static List<int> MatchAnchor(IList<string> texts, string wanted)
    {
        var found = new List<int>();
        if (string.IsNullOrEmpty(wanted) || wanted.Trim().Length == 0)
            return found;
        for (int pass = 0; pass < 3 && found.Count == 0; pass++)
        {
            string target = pass == 0 ? wanted : pass == 1 ? wanted.Trim() : Plain(wanted);
            for (int i = 0; i < texts.Count; i++)
            {
                string text = texts[i] ?? "";
                bool match = pass == 0 ? text == target
                    : pass == 1 ? string.Equals(text.Trim(), target, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(Plain(text), target, StringComparison.OrdinalIgnoreCase);
                if (match)
                    found.Add(i);
            }
        }
        return found;
    }

    /// <summary>A topic: a player line that shows when <paramref name="showWhen"/> holds, followed by the NPC's lines.</summary>
    public static ConversationDefinition Topic(string modId, string conversation, int anchorId, string playerLine, string[] npcLines, string showWhen)
    {
        var definition = new ConversationDefinition { Key = "topic" + HashedKey(modId + "\n" + conversation + "\n" + anchorId + "\n" + playerLine) };
        definition.Lines.Add(new ConversationLine { Key = "topic", Player = true, Text = playerLine, Condition = showWhen, Next = new[] { "reply1" } });
        for (int i = 0; i < npcLines.Length; i++)
        {
            definition.Lines.Add(new ConversationLine
            {
                Key = "reply" + (i + 1),
                Text = npcLines[i],
                Next = i + 1 < npcLines.Length ? new[] { "reply" + (i + 2) } : null,
            });
        }
        return definition;
    }
}
