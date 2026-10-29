// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenUIKit.Input;
using ObenUIKit.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ObenUIKit.Preview;

/// <summary>
/// The picture of a model preview in a window. Drag to turn the model, scroll to zoom; with the keyboard,
/// left and right turn it. A frame is drawn only when something changed or the model spins, and only
/// while the picture is on screen (a hidden tab or a closed window draws nothing).
/// </summary>
internal sealed class KitModelView : MonoBehaviour, IDragHandler, IScrollHandler, IPointerEnterHandler, IPointerExitHandler, INavTarget
{
    private const float DegreesPerPixel = 0.4f;
    private const float KeyTurn = 20f;
    private const float ZoomStep = 0.1f;

    private ModelStage _stage;
    private RawImage _image;
    private Image _frame;
    private KitContext _kit;
    private UiNav _nav;
    private bool _dirty = true;
    private bool _hover;
    private bool _focus;

    /// <summary>Degrees per second the model turns by itself; 0 keeps it still.</summary>
    public float Spin { get; set; }

    public RectTransform Rect => (RectTransform)transform;

    public bool Focusable => isActiveAndEnabled;

    internal ModelStage Stage => _stage;

    internal void Init(KitContext kit, UiNav nav, ModelStage stage, RawImage image, Image frame)
    {
        _kit = kit;
        _nav = nav;
        _stage = stage;
        _image = image;
        _frame = frame;
        Paint();
    }

    /// <summary>Asks for a new frame on the next LateUpdate.</summary>
    public void Redraw() => _dirty = true;

    private void OnEnable() => _dirty = true;

    private void OnRectTransformDimensionsChange() => _dirty = true;

    private void LateUpdate()
    {
        // A live subject moves by itself (the game animates it), so it is drawn every frame.
        if (_stage == null || (!_dirty && Spin == 0f && !_stage.IsLive))
            return;
        if (Spin != 0f)
        {
            var rotation = _stage.Rotation;
            rotation.y += Spin * Time.unscaledDeltaTime;
            _stage.Rotation = rotation;
        }
        var rect = Rect.rect;
        // Not laid out yet: try again next frame.
        if (rect.width < 1f || rect.height < 1f)
            return;
        _dirty = false;
        // The texture is drawn at the size the picture really has on screen.
        float scale = _image.canvas != null ? _image.canvas.scaleFactor : 1f;
        if (_stage.Resize(Mathf.RoundToInt(rect.width * scale), Mathf.RoundToInt(rect.height * scale)))
            _image.texture = _stage.Texture;
        if (_stage.HasModel)
            _stage.Render();
        _image.enabled = _stage.HasModel;
    }

    public void OnDrag(PointerEventData data)
    {
        var rotation = _stage.Rotation;
        rotation.y -= data.delta.x * DegreesPerPixel;
        rotation.x = Mathf.Clamp(rotation.x + data.delta.y * DegreesPerPixel, -80f, 80f);
        _stage.Rotation = rotation;
        _dirty = true;
    }

    public void OnScroll(PointerEventData data)
    {
        _stage.Zoom = Mathf.Clamp(_stage.Zoom * (1f - data.scrollDelta.y * ZoomStep), 0.3f, 3f);
        _dirty = true;
    }

    public void OnPointerEnter(PointerEventData data)
    {
        _hover = true;
        _nav?.Hover(this);
        Paint();
    }

    public void OnPointerExit(PointerEventData data)
    {
        _hover = false;
        _nav?.Unhover(this);
        Paint();
    }

    public void SetFocus(bool on)
    {
        _focus = on;
        Paint();
    }

    /// <summary>Enter puts the model back the way it started.</summary>
    public void Activate()
    {
        _stage.Rotation = _stage.StartRotation;
        _stage.Zoom = 1f;
        _dirty = true;
    }

    public bool Step(int delta)
    {
        var rotation = _stage.Rotation;
        rotation.y -= delta * KeyTurn;
        _stage.Rotation = rotation;
        _dirty = true;
        return true;
    }

    private void Paint()
    {
        if (_frame != null && _kit != null)
            _frame.color = _hover || _focus ? _kit.Theme.RowHover : _kit.Theme.PanelEdge;
    }

    private void OnDestroy()
    {
        _stage?.Dispose();
        _stage = null;
    }
}
