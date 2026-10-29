using ObenFramework.Assets;
using ObenFramework.Items;
using UnityEngine;

namespace ObenUIKit.Preview;

/// <summary>
/// A 3D model preview in a window. Show a mod's model (<see cref="ModelTemplate"/> from
/// <see cref="ModelRegistry"/>), any object or prefab (only its look is copied), a game item, or the player (or
/// another object in the scene) live, posed and animated by the game, framed whole or by the face.
/// It goes away with its window.
/// </summary>
public sealed class ModelPreviewHandle
{
    /// <summary>The control's row (or its whole box), to add uGUI of your own to it or adjust its layout.</summary>
    public RectTransform Rect { get; internal set; }

    private readonly KitModelView _view;
    private readonly FramingSwitch _switch;

    internal ModelPreviewHandle(KitModelView view, FramingSwitch framingSwitch)
    {
        _view = view;
        _switch = framingSwitch;
        _switch?.Show(PreviewFraming.Body);
    }

    private ModelStage Stage => _view != null ? _view.Stage : null;

    /// <summary>Something is on show.</summary>
    public bool HasModel => Stage != null && Stage.HasModel;

    /// <summary>How many renderers are on show (0 when nothing is): a quick check that a model or outfit has parts.</summary>
    public int Renderers => Stage != null ? Stage.Renderers : 0;

    /// <summary>The picture the preview draws into, to show it somewhere else too. Null until it has been drawn once.</summary>
    public Texture Texture => Stage != null ? Stage.Texture : null;

    /// <summary>The whole model, or a character's head and shoulders.</summary>
    public PreviewFraming Framing
    {
        get => Stage != null ? Stage.Framing : PreviewFraming.Body;
        set
        {
            if (Stage == null)
                return;
            Stage.Framing = value;
            _switch?.Show(value);
            _view.Redraw();
        }
    }

    /// <summary>"Body" and "Face" in the preview's corner for the player to switch the framing. Off by default.</summary>
    public bool ShowFramingSwitch
    {
        get => _switch != null && _switch.Visible;
        set
        {
            if (_switch != null)
                _switch.Visible = value;
        }
    }

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

    /// <summary>
    /// Shows the player as they are, live: the game keeps animating them and whatever they wear shows. Their body
    /// leaves the game's view while the preview shows it (also in first person) and comes back after. Returns
    /// false outside a level. One live preview at a time: a second takes the player over.
    /// </summary>
    public bool ShowPlayer()
    {
        if (Stage == null)
            return false;
        var subject = LiveSubject.Player(Stage.Layer);
        if (subject == null)
        {
            Clear();
            return false;
        }
        Stage.ShowLive(subject);
        _view.Redraw();
        return true;
    }

    /// <summary>Shows an object in the scene live, where it stands (an NPC, say), turned from its own front.</summary>
    public void ShowLive(GameObject subject)
    {
        if (Stage == null)
            return;
        var live = LiveSubject.Of(subject, Stage.Layer);
        if (live == null)
        {
            Clear();
            return;
        }
        Stage.ShowLive(live);
        _view.Redraw();
    }

    /// <summary>
    /// Copies the shown object again (after you changed it), or picks up a live subject's new parts (after an
    /// outfit change), keeping the turn and zoom.
    /// </summary>
    public void Refresh()
    {
        if (Stage == null)
            return;
        Stage.Refresh();
        _view.Redraw();
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
