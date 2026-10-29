using UnityEngine;

namespace ObenUIKit.Preview;

/// <summary>Where the camera goes for a copy on the stage or a live subject in the world, and how big a copy is.</summary>
internal sealed partial class ModelStage
{
    // A copy hangs on the pivot, which turns it about the point the framing looks at.
    private void PlaceCopy(float fit, float zoom)
    {
        _pivot.localRotation = Quaternion.Euler(Rotation.x, Rotation.y, 0f);
        var focus = _centre;
        float reach = _radius;
        if (Framing == PreviewFraming.Face)
        {
            focus = new Vector3(_centre.x, _top - _size.y * FaceDrop, _centre.z);
            reach = Mathf.Max(0.02f, _size.y * FaceHalf);
        }
        if (_model != null)
            _model.transform.localPosition = -focus;
        float distance = reach / Mathf.Sin(fit) * zoom;
        _camera.transform.localPosition = new Vector3(0f, 0f, -distance);
        _camera.transform.localRotation = Quaternion.identity;
        _camera.farClipPlane = distance + _radius * 2f + 1f;
        _camera.nearClipPlane = Mathf.Max(0.01f, (distance - _radius) * 0.5f);
    }

    // A live subject stays put; the camera circles it, starting in front of it, measured again every frame.
    private bool PlaceLive(float fit, float zoom)
    {
        if (!_live.Alive || !_live.Measure(out var bounds))
            return false;
        float height = Mathf.Max(0.05f, bounds.size.y);
        var focus = bounds.center;
        float reach = height * 0.5f / BodyFill;
        if (Framing == PreviewFraming.Face)
        {
            focus = new Vector3(bounds.center.x, bounds.max.y - height * FaceDrop, bounds.center.z);
            reach = height * FaceHalf;
        }
        float distance = reach / Mathf.Tan(fit) * zoom;
        // The camera circles the subject instead of the subject turning, so both angles go the other way round:
        // a drag to the right still moves the side facing the viewer to the right, like a copy turning.
        var turn = Quaternion.LookRotation(_live.Forward) * Quaternion.Euler(-Rotation.x, 180f - Rotation.y, 0f);
        _camera.transform.SetPositionAndRotation(focus - turn * Vector3.forward * distance, turn);
        _camera.nearClipPlane = Mathf.Max(0.02f, distance - height);
        _camera.farClipPlane = distance + height * 2f + 1f;
        return true;
    }

    // Centre, size and top of the model with no rotation, so it turns about its middle.
    private void Measure()
    {
        _pivot.localRotation = Quaternion.identity;
        _model.transform.localPosition = Vector3.zero;
        var renderers = _model.GetComponentsInChildren<Renderer>(true);
        _count = renderers.Length;
        bool any = false;
        var bounds = new Bounds();
        foreach (var renderer in renderers)
        {
            var b = renderer.bounds;
            if (b.size.sqrMagnitude < 1e-8f)
                continue;
            if (any)
                bounds.Encapsulate(b);
            else
                bounds = b;
            any = true;
        }
        if (!any)
        {
            _centre = Vector3.zero;
            _size = Vector3.one;
            _top = 0.5f;
            _radius = 0.5f;
            return;
        }
        _centre = _pivot.InverseTransformPoint(bounds.center);
        _size = bounds.size;
        _top = _centre.y + bounds.extents.y;
        _radius = Mathf.Max(0.05f, bounds.extents.magnitude);
    }
}
