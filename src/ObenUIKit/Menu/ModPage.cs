using System;
using ObenUIKit.Controls;

namespace ObenUIKit.Menu;

/// <summary>A page in the mod menu. Pages are listed by <see cref="Order"/>, then title.</summary>
public sealed class ModPage
{
    public ModPage(string title, Action<UiBuilder> build)
    {
        Title = title;
        Build = build;
    }

    public string Title { get; }
    public Action<UiBuilder> Build { get; }
    public int Order { get; set; } = 100;
}
