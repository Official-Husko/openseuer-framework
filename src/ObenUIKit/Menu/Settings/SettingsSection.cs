using System;
using ObenLoader.Config;

namespace ObenUIKit.Menu.Settings;

/// <summary>One config file on the Settings page and how to write it back.</summary>
public sealed class SettingsSection
{
    private readonly Func<object> _value;
    private readonly Action _save;
    private readonly Func<object> _defaults;
    private bool _dirty;

    internal SettingsSection(string title, string ownerId, Func<object> value, Action save, Func<object> defaults,
        IConfigFile source = null)
    {
        Title = title;
        OwnerId = ownerId;
        _value = value;
        _save = save;
        _defaults = defaults;
        Source = source;
    }

    public string Title { get; }

    /// <summary>The id of the mod the file belongs to.</summary>
    public string OwnerId { get; }

    /// <summary>The settings object as it is now (the config's value, which changes on a reload).</summary>
    public object Value => _value();

    /// <summary>
    /// False for a file the page found by itself among a mod's loaded configs; true for one a mod registered
    /// with <see cref="ModSettings.Register{T}"/>.
    /// </summary>
    public bool Registered { get; internal set; } = true;

    /// <summary>The config file behind the section.</summary>
    internal IConfigFile Source { get; }

    /// <summary>A fresh object with the default values.</summary>
    internal object Defaults => _defaults();

    /// <summary>
    /// Called after a field was edited. Sliders pass <paramref name="deferred"/> so a drag writes the
    /// file once it settles instead of on every step; see <see cref="Flush"/>.
    /// </summary>
    internal void Edited(bool deferred)
    {
        if (deferred)
            _dirty = true;
        else
            Save();
    }

    /// <summary>Writes the file if an edit is waiting for it.</summary>
    internal void Flush()
    {
        if (_dirty)
            Save();
    }

    /// <summary>
    /// Puts every value the page shows back to its default and writes the file. Hidden fields (the mod's own
    /// bookkeeping) keep theirs.
    /// </summary>
    internal void Reset()
    {
        object value = Value;
        object defaults = Defaults;
        foreach (var view in FieldView.For(value.GetType()))
            view.Field.SetValue(value, view.Field.GetValue(defaults));
        Save();
    }

    /// <summary>Puts one field back to its default and writes the file.</summary>
    internal void Reset(FieldView view)
    {
        view.Field.SetValue(Value, view.Field.GetValue(Defaults));
        Save();
    }

    private void Save()
    {
        _dirty = false;
        _save();
    }
}
