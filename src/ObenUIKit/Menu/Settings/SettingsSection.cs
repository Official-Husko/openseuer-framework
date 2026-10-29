using System;

namespace ObenUIKit.Menu.Settings;

/// <summary>One registered config file and how to write it back.</summary>
public sealed class SettingsSection
{
    private readonly Func<object> _value;
    private readonly Action _save;
    private readonly Func<object> _defaults;
    private bool _dirty;

    internal SettingsSection(string title, string ownerId, Func<object> value, Action save, Func<object> defaults)
    {
        Title = title;
        OwnerId = ownerId;
        _value = value;
        _save = save;
        _defaults = defaults;
    }

    public string Title { get; }

    /// <summary>The id of the mod that registered it.</summary>
    public string OwnerId { get; }

    /// <summary>The settings object as it is now (the config's value, which changes on a reload).</summary>
    public object Value => _value();

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
        object defaults = _defaults();
        foreach (var view in FieldView.For(value.GetType()))
            view.Field.SetValue(value, view.Field.GetValue(defaults));
        Save();
    }

    private void Save()
    {
        _dirty = false;
        _save();
    }
}
