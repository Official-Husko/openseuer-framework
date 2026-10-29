// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using UnityEngine;

namespace ObenUIKit.Preview;

/// <summary>
/// A tiny photo studio far below the world for one model preview: a pivot the model hangs on, a camera
/// and three lamps that only see the preview layer, and the render texture they draw into. The camera is
/// never enabled; <see cref="Render"/> draws one frame on demand, with the lamps switched on just for
/// that, so several previews never light each other and nothing renders while nothing changes. A live
/// subject (<see cref="LiveSubject"/>) stays where it is in the world; the camera and lamps go to it.
/// </summary>
internal sealed partial class ModelStage : IDisposable
{
    public const string RootName = "ObenUIKit model preview";

    private const float Fov = 30f;
    // The reference's framing: a body fills 85% of the height; a face is centred 11% of the height below the
    // top, with 16.5% of the height above and below.
    private const float BodyFill = 0.85f;
    private const float FaceDrop = 0.11f;
    private const float FaceHalf = 0.165f;
    private static readonly Vector2 CopyStart = new Vector2(10f, 200f);
    private static readonly Vector2 LiveStart = new Vector2(-6f, 0f);
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
    private GameObject _source;
    private LiveSubject _live;
    private Vector3 _centre;
    private Vector3 _size;
    private float _top;
    private float _radius = 0.5f;
    private int _count;
    private bool _disposed;

    public ModelStage()
    {
        _layer = PreviewLayer.Get();
        _root = new GameObject(RootName);
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

    public bool HasModel => _model != null || _live != null && _live.Alive;

    /// <summary>A live subject is on show: it moves, so it is drawn every frame.</summary>
    public bool IsLive => _live != null && _live.Alive;

    /// <summary>How many renderers are on show.</summary>
    public int Renderers => _live != null ? _live.Count : _model != null ? _count : 0;

    /// <summary>The preview layer, for <see cref="LiveSubject"/>.</summary>
    public int Layer => _layer;

    public PreviewFraming Framing { get; set; }

    /// <summary>How the model is turned, in degrees: x tilts, y spins (for a live subject, from its front).</summary>
    public Vector2 Rotation { get; set; } = CopyStart;

    /// <summary>The turn a model starts at (and Enter on the preview goes back to).</summary>
    public Vector2 StartRotation => _live != null ? LiveStart : CopyStart;

    /// <summary>1 frames the whole model; below 1 is closer.</summary>
    public float Zoom { get; set; } = 1f;

    /// <summary>Puts a look-only copy of <paramref name="source"/> on the stage, replacing what was there.</summary>
    public void Show(GameObject source)
    {
        Clear();
        if (source == null)
            return;
        _source = source;
        _model = PreviewCopy.Make(source, _pivot, _layer);
        Measure();
    }

    /// <summary>Shows a live subject where it stands, replacing what was there; the stage owns it from now on.</summary>
    public void ShowLive(LiveSubject subject)
    {
        Clear();
        _live = subject;
        Rotation = LiveStart;
    }

    /// <summary>Copies the shown object again, or picks up a live subject's new parts (after an outfit change).</summary>
    public void Refresh()
    {
        if (_live != null)
        {
            _live.Refresh();
            return;
        }
        if (_source == null)
            return;
        var rotation = Rotation;
        Show(_source);
        Rotation = rotation;
    }

    public void Clear()
    {
        if (_model != null)
            UnityEngine.Object.Destroy(_model);
        _model = null;
        _source = null;
        _live?.Dispose();
        _live = null;
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
        float aspect = (float)_texture.width / _texture.height;
        float half = Fov * 0.5f * Mathf.Deg2Rad;
        // What has to fit goes in the narrower of the two directions.
        float fit = aspect < 1f ? Mathf.Atan(Mathf.Tan(half) * aspect) : half;
        float zoom = Mathf.Clamp(Zoom, 0.2f, 4f);
        if (_live != null)
        {
            if (!PlaceLive(fit, zoom))
                return;
        }
        else
        {
            PlaceCopy(fit, zoom);
        }
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
