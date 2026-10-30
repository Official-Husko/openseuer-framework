// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Saves;
using ObenFramework.Scenes;
using ObenFramework.Patching;

namespace ObenFramework.World;

/// <summary>An editable sign and its text.</summary>
public readonly struct SignInfo
{
    public SignInfo(string id, string text, int maxLines, int maxLength)
    {
        Id = id;
        Text = text;
        MaxLines = maxLines;
        MaxLength = maxLength;
    }

    /// <summary>Id for <see cref="Signs.Get"/> and <see cref="Signs.SetText"/>, see <see cref="SceneIds"/>.</summary>
    public string Id { get; }

    public string Text { get; }
    public int MaxLines { get; }

    /// <summary>Most characters the sign takes; 0 means no limit.</summary>
    public int MaxLength { get; }
}

/// <summary>Raised by <see cref="Signs.TextChanged"/>.</summary>
public readonly struct SignTextChanged
{
    public SignTextChanged(string id, string text, bool byPlayer)
    {
        Id = id;
        Text = text;
        ByPlayer = byPlayer;
    }

    public string Id { get; }
    public string Text { get; }

    /// <summary>The player typed it; false when a mod set it.</summary>
    public bool ByPlayer { get; }
}

/// <summary>
/// The signs the player can write on (<c>EditableSign</c>): read them, set their text, and hear when the
/// text changes, whether the player finished typing or a mod set it. The game saves the text itself.
/// </summary>
public static class Signs
{
    private static EditableSign _edited;
    private static string _editStartText;

    /// <summary>A sign's text changed. For the player, raised once when they stop typing, if the text differs.</summary>
    public static readonly WorldEvent<SignTextChanged> TextChanged = new WorldEvent<SignTextChanged>(nameof(TextChanged));

    /// <summary>False when the framework's sign hooks failed to apply after a game update; <see cref="TextChanged"/> then stays quiet.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "World");

    public static List<SignInfo> All()
    {
        var list = new List<SignInfo>();
        foreach (var sign in SceneIds.Active<EditableSign>())
            list.Add(Info(sign));
        return list;
    }

    /// <summary>The sign with this id in the active scene, or null.</summary>
    public static SignInfo? Get(string id)
    {
        var sign = SceneIds.Find<EditableSign>(id);
        return sign != null ? Info(sign) : (SignInfo?)null;
    }

    /// <summary>
    /// Sets a sign's text. Refused (false) while the player is editing that sign, while the game saves or
    /// loads, or when the text is longer or has more lines than the sign takes.
    /// </summary>
    public static bool SetText(string id, string text)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        if (SaveGate.IsBusy)
            return false;
        try
        {
            var sign = SceneIds.Find<EditableSign>(id);
            if (sign == null || !Accepts(sign, text))
                return false;
            string before = sign.text;
            sign.text = text;
            sign.UpdateText();
            if (before != text)
                TextChanged.Raise(new SignTextChanged(SceneIds.Of(sign), text, false));
            return true;
        }
        catch (Exception e)
        {
            WorldLog.Failed("Signs.SetText", e);
            return false;
        }
    }

    internal static void BeginEdit(EditableSign sign)
    {
        _edited = sign;
        _editStartText = sign.text;
    }

    internal static void EndEdit(EditableSign sign)
    {
        if (_edited != sign)
            return;
        bool changed = sign.text != _editStartText;
        _edited = null;
        _editStartText = null;
        if (changed)
            TextChanged.Raise(new SignTextChanged(SceneIds.Of(sign), sign.text, true));
    }

    internal static void Reset()
    {
        _edited = null;
        _editStartText = null;
    }

    private static bool Accepts(EditableSign sign, string text)
    {
        // The input field is only interactable while the player types in it.
        if (sign.textComponent == null || sign.textComponent.interactable)
            return false;
        int limit = sign.textComponent.characterLimit;
        if (limit > 0 && text.Length > limit)
            return false;
        return text.Split('\n').Length <= Math.Max(sign.maxLines, 1);
    }

    private static SignInfo Info(EditableSign sign)
    {
        int maxLength = sign.textComponent != null ? sign.textComponent.characterLimit : 0;
        return new SignInfo(SceneIds.Of(sign), sign.text ?? "", sign.maxLines, maxLength);
    }
}
