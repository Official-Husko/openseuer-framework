using ObenFramework.Assets;
using ObenUIKit.Preview;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Controls;

/// <summary>3D model previews.</summary>
public sealed partial class UiBuilder
{
    public const float DefaultPreviewHeight = 360f;

    /// <summary>
    /// A box that shows a 3D model, lit and turning in the light, drawn by a camera of its own far from the
    /// world. Drag to turn it, scroll to zoom, Enter puts it back. It draws only when something changed (or
    /// while it spins) and only while it's on screen; everything it made goes away with the window.
    /// Show something with <see cref="ModelPreviewHandle.Show(ModelTemplate)"/>, <c>Show(GameObject)</c> or
    /// <see cref="ModelPreviewHandle.ShowItem"/>.
    /// </summary>
    public ModelPreviewHandle ModelPreview(float height = DefaultPreviewHeight)
    {
        var frame = UiParts.Image(_parent, "Model preview", null, Theme.PanelEdge);
        UiParts.Height(frame, height);
        var backdrop = UiParts.Image(frame.transform, "Backdrop", null, Theme.Header);
        UiParts.Stretch(backdrop.rectTransform, 3f, 3f, 3f, 3f);
        var picture = UiParts.Child(backdrop.transform, "Picture");
        UiParts.Stretch(picture);
        var image = picture.gameObject.AddComponent<RawImage>();
        image.raycastTarget = true;
        image.enabled = false;

        var stage = new ModelStage();
        var view = picture.gameObject.AddComponent<KitModelView>();
        view.Init(_kit, _window.Nav, stage, image, frame);
        // The window disposes the stage when it closes; the view does too if it is destroyed first (a rebuild).
        _window.Track(stage);
        _window.Nav.Add(view, _inBody, _group);
        _last = frame.rectTransform;
        return new ModelPreviewHandle(view);
    }

    /// <summary>A model preview showing a model a mod loaded with <see cref="ModelRegistry"/>.</summary>
    public ModelPreviewHandle ModelPreview(ModelTemplate model, float height = DefaultPreviewHeight)
    {
        var handle = ModelPreview(height);
        handle.Show(model);
        return handle;
    }

    /// <summary>A model preview showing the look of <paramref name="source"/> (a prefab or a scene object; only its meshes are copied).</summary>
    public ModelPreviewHandle ModelPreview(GameObject source, float height = DefaultPreviewHeight)
    {
        var handle = ModelPreview(height);
        handle.Show(source);
        return handle;
    }
}
