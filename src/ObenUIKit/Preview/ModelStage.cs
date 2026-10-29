// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using UnityEngine;

namespace ObenUIKit.Preview;

/// <summary>
/// A tiny photo studio far below the world for one model preview: a pivot the model hangs on, a camera
/// and three lamps that only see the preview layer, and the render texture they draw into. The camera is
/// never enabled; <see cref="Render"/> draws one frame on demand, with the lamps switched on just for
/// that, so several previews never light each other and nothing renders while nothing changes.
/// </summary>
internal sealed class ModelStage : IDisposable
{
    private const float Fov = 30f;
    private const float StageDepth = -12000f;
    private const float StageSpacing = 200f;

    private static readonly Vector3[] LampAngles = { new Vector3(35f, -40f, 0f), new Vector3(10f, 60f, 0f), new Vector3(-20f, 180f, 0f) };
    private static readonly float[] LampIntensity = { 1.1f, 0.45f, 0.6f };

    private static int _slots;

    private readonly GameObject _root;
    private readonly Transform _pivot;
    private readonly Camera _camera;
    private readonly Light[] _lamps;
    private readonly int _layer;
    private RenderTexture _texture;
    private GameObject _model;
    private Vector3 _centre;
    private float _radius = 0.5f;
    private bool _disposed;

    public ModelStage()
    {
        _layer = PreviewLayer.Get();
        _root = new GameObject("ObenUIKit model preview");
        UnityEngine.Object.DontDestroyOnLoad(_root);
        _root.transform.position = new Vector3(_slots++ % 64 * StageSpacing, StageDepth, 0f);

        _pivot = new GameObject("Pivot").transform;
        _pivot.SetParent(_root.transform, false);

        var eye = new GameObject("Camera");
        eye.transform.SetParent(_root.transform, false);
        _camera = eye.AddComponent<Camera>();
        _camera.enabled = false;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        _camera.cullingMask = 1 << _layer;
        _camera.fieldOfView = Fov;
        _camera.nearClipPlane = 0.02f;
        _camera.allowHDR = false;
        _camera.useOcclusionCulling = false;

        _lamps = new Light[LampAngles.Length];
        for (int i = 0; i < _lamps.Length; i++)
        {
            var lamp = new GameObject("Lamp " + i).AddComponent<Light>();
            lamp.transform.SetParent(eye.transform, false);
            lamp.transform.localRotation = Quaternion.Euler(LampAngles[i]);
            lamp.type = LightType.Directional;
            lamp.intensity = LampIntensity[i];
            lamp.color = i == 0 ? new Color(1f, 0.95f, 0.88f) : new Color(0.85f, 0.9f, 1f);
            lamp.shadows = LightShadows.None;
            lamp.cullingMask = 1 << _layer;
            lamp.enabled = false;
            _lamps[i] = lamp;
        }
        PreviewLayer.Acquire();
    }

    public RenderTexture Texture => _texture;

    public bool HasModel => _model != null;

    /// <summary>How the model is turned, in degrees: x tilts, y spins.</summary>
    public Vector2 Rotation { get; set; } = new Vector2(10f, 200f);

    /// <summary>1 frames the whole model; below 1 is closer.</summary>
    public float Zoom { get; set; } = 1f;

    /// <summary>Puts a look-only copy of <paramref name="source"/> on the stage, replacing what was there.</summary>
    public void Show(GameObject source)
    {
        Clear();
        if (source == null)
            return;
        _model = PreviewCopy.Make(source, _pivot, _layer);
        Measure();
    }

    public void Clear()
    {
        if (_model != null)
            UnityEngine.Object.Destroy(_model);
        _model = null;
    }

    /// <summary>Makes sure the texture matches the size the preview is drawn at. Returns true when it was remade.</summary>
    public bool Resize(int width, int height)
    {
        // The view can still run its LateUpdate in the frame the window closes; a texture made then would never be released.
        if (_disposed)
            return false;
        width = Mathf.Clamp(width, 32, 2048);
        height = Mathf.Clamp(height, 32, 2048);
        if (_texture != null && _texture.width == width && _texture.height == height)
            return false;
        Release();
        _texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "ObenUIKit preview", antiAliasing = 4 };
        _texture.Create();
        _camera.targetTexture = _texture;
        return true;
    }

    /// <summary>Draws one frame of the model into the texture.</summary>
    public void Render()
    {
        if (_disposed || _texture == null)
            return;
        _pivot.localRotation = Quaternion.Euler(Rotation.x, Rotation.y, 0f);
        if (_model != null)
            _model.transform.localPosition = -_centre;

        float aspect = (float)_texture.width / _texture.height;
        float half = Fov * 0.5f * Mathf.Deg2Rad;
        // Fit the model's bounding sphere in the narrower of the two directions.
        float fit = aspect < 1f ? Mathf.Atan(Mathf.Tan(half) * aspect) : half;
        float distance = _radius / Mathf.Sin(fit) * Mathf.Clamp(Zoom, 0.2f, 4f);
        _camera.transform.localPosition = new Vector3(0f, 0f, -distance);
        _camera.transform.localRotation = Quaternion.identity;
        _camera.farClipPlane = distance + _radius * 2f + 1f;
        _camera.nearClipPlane = Mathf.Max(0.01f, (distance - _radius) * 0.5f);
        _camera.aspect = aspect;

        for (int i = 0; i < _lamps.Length; i++)
            _lamps[i].enabled = true;
        PreviewLayer.Isolate();
        try
        {
            _camera.Render();
        }
        finally
        {
            for (int i = 0; i < _lamps.Length; i++)
                _lamps[i].enabled = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Clear();
        Release();
        if (_root != null)
            UnityEngine.Object.Destroy(_root);
        PreviewLayer.Release();
    }

    // Centre and size of the model with no rotation, so it turns about its middle.
    private void Measure()
    {
        _pivot.localRotation = Quaternion.identity;
        _model.transform.localPosition = Vector3.zero;
        var renderers = _model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            _centre = Vector3.zero;
            _radius = 0.5f;
            return;
        }
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        _centre = _pivot.InverseTransformPoint(bounds.center);
        _radius = Mathf.Max(0.05f, bounds.extents.magnitude);
    }

    private void Release()
    {
        if (_camera != null)
            _camera.targetTexture = null;
        if (_texture != null)
        {
            _texture.Release();
            UnityEngine.Object.Destroy(_texture);
        }
        _texture = null;
    }
}
