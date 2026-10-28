using ObenFramework.Assets;
using ObenFramework.Items;
using UnityEngine;

namespace ObenUIKit.Preview;

/// <summary>
/// A 3D model preview in a window. Show a mod's model (<see cref="ModelTemplate"/> from
/// <see cref="ModelRegistry"/>), any object or prefab (only its look is copied), or a game item.
/// It goes away with its window.
/// </summary>
public sealed class ModelPreviewHandle
{
    private readonly KitModelView _view;

    internal ModelPreviewHandle(KitModelView view)
    {
        _view = view;
    }

    private ModelStage Stage => _view != null ? _view.Stage : null;

    /// <summary>Something is on show.</summary>
    public bool HasModel => Stage != null && Stage.HasModel;

    /// <summary>Degrees per second the model turns by itself (try 30); 0 keeps it still. It only draws every frame while it spins.</summary>
    public float Spin
    {
        get => _view != null ? _view.Spin : 0f;
        set
        {
            if (_view != null)
                _view.Spin = value;
        }
    }

    /// <summary>How the model is turned, in degrees: x tilts it towards the viewer, y spins it.</summary>
    public Vector2 Rotation
    {
        get => Stage != null ? Stage.Rotation : Vector2.zero;
        set
        {
            if (Stage == null)
                return;
            Stage.Rotation = value;
            _view.Redraw();
        }
    }

    /// <summary>1 fits the whole model; smaller is closer (0.3 to 3).</summary>
    public float Zoom
    {
        get => Stage != null ? Stage.Zoom : 1f;
        set
        {
            if (Stage == null)
                return;
            Stage.Zoom = Mathf.Clamp(value, 0.3f, 3f);
            _view.Redraw();
        }
    }

    /// <summary>Shows a model a mod loaded with <see cref="ModelRegistry"/>.</summary>
    public void Show(ModelTemplate model)
    {
        if (model == null || !model.IsAlive)
        {
            Clear();
            return;
        }
        Show(model.Prefab);
    }

    /// <summary>Shows a copy of an object's look: its meshes and materials, none of its scripts.</summary>
    public void Show(GameObject source)
    {
        if (Stage == null)
            return;
        Stage.Show(source);
        _view.Redraw();
    }

    /// <summary>Shows a game item's world model. Returns false when the item has none.</summary>
    public bool ShowItem(int itemId)
    {
        var prefab = ItemPrefab(itemId);
        if (prefab == null)
        {
            Clear();
            return false;
        }
        Show(prefab);
        return true;
    }

    public void Clear()
    {
        if (Stage == null)
            return;
        Stage.Clear();
        _view.Redraw();
    }

    // The game loads an item's prefab from its resources the first time it is needed; do the same.
    internal static GameObject ItemPrefab(int itemId)
    {
        var item = ItemLookup.ById(itemId);
        var look = item != null ? item.Appearance : null;
        if (look == null)
            return null;
        if (look.Prefab == null && !string.IsNullOrEmpty(look.PrefabPath))
            look.Loadprefab(item);
        return look.Prefab;
    }
}
