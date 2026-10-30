using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ObenFramework.Assets;

/// <summary>
/// The scene for one icon: a copy of a model's meshes (no components but mesh renderers, so none of its
/// scripts wake), two lights and a camera, all on one layer far above the level, and gone again in the same
/// frame. While it takes its pictures the game's lights leave that layer and fog and ambient light are set
/// to plain values, and everything is put back right after.
/// </summary>
internal sealed class IconStage : IDisposable
{
    private static readonly Vector3 Place = new Vector3(0f, 6000f, 0f);

    private readonly int _layer;
    private readonly GameObject _root;
    private readonly List<Renderer> _parts = new List<Renderer>();
    private Camera _camera;

    public IconStage(int layer)
    {
        _layer = layer;
        _root = new GameObject("Model icon") { hideFlags = HideFlags.HideAndDontSave };
        _root.transform.position = Place;
    }

    /// <summary>Copies the visible meshes of <paramref name="source"/>. False when it has none.</summary>
    public bool Copy(GameObject source)
    {
        var skipped = OtherLods(source);
        var origin = source.transform;
        foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled || skipped.Contains(renderer) || !Shown(renderer.transform, origin))
                continue;
            var mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                : renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
            if (mesh == null)
                continue;

            var part = new GameObject(renderer.name) { layer = _layer, hideFlags = HideFlags.HideAndDontSave };
            var relative = origin.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            part.transform.SetParent(_root.transform, false);
            part.transform.localPosition = relative.GetColumn(3);
            part.transform.localRotation = relative.rotation;
            part.transform.localScale = relative.lossyScale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var copy = part.AddComponent<MeshRenderer>();
            copy.sharedMaterials = renderer.sharedMaterials;
            copy.shadowCastingMode = ShadowCastingMode.Off;
            copy.receiveShadows = false;
            copy.lightProbeUsage = LightProbeUsage.Off;
            copy.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (renderer.HasPropertyBlock())
            {
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                copy.SetPropertyBlock(block);
            }
            _parts.Add(copy);
        }
        return _parts.Count > 0;
    }

    /// <summary>Points the camera at the copy from the angle asked for, so the model fills the picture but for the margin.</summary>
    public void Frame(float yaw, float pitch, float margin)
    {
        var view = Quaternion.Euler(pitch, yaw + 180f, 0f);
        var toView = Quaternion.Inverse(view);
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (var part in _parts)
        {
            var bounds = part.GetComponent<MeshFilter>().sharedMesh.bounds;
            var matrix = part.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                var local = new Vector3(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                var seen = toView * (matrix.MultiplyPoint3x4(local) - Place);
                min = Vector3.Min(min, seen);
                max = Vector3.Max(max, seen);
            }
        }

        float half = Mathf.Max(max.x - min.x, max.y - min.y, 0.001f) / 2f / (1f - 2f * margin);
        var holder = new GameObject("Icon camera") { hideFlags = HideFlags.HideAndDontSave };
        holder.transform.SetParent(_root.transform, false);
        holder.transform.SetPositionAndRotation(Place + view * new Vector3((min.x + max.x) / 2f, (min.y + max.y) / 2f, min.z - 1f), view);
        _camera = holder.AddComponent<Camera>();
        _camera.enabled = false;
        _camera.orthographic = true;
        _camera.orthographicSize = half;
        _camera.nearClipPlane = 0.01f;
        _camera.farClipPlane = max.z - min.z + 2f;
        _camera.cullingMask = 1 << _layer;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.renderingPath = RenderingPath.Forward;
        _camera.allowHDR = false;
        _camera.allowMSAA = false;
        _camera.useOcclusionCulling = false;

        AddLight(view * Quaternion.Euler(40f, 35f, 0f), 1.0f);
        AddLight(view * Quaternion.Euler(15f, -140f, 0f), 0.35f);
    }

    /// <summary>
    /// Draws the copy over black and over white at <paramref name="side"/> pixels, as linear RGBA floats
    /// (bottom row first).
    /// </summary>
    public (float[] overBlack, float[] overWhite) Shoot(int side)
    {
        var target = new RenderTexture(side, side, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear) { hideFlags = HideFlags.HideAndDontSave };
        var readback = new Texture2D(side, side, TextureFormat.RGBAFloat, false, true) { hideFlags = HideFlags.HideAndDontSave };
        var lights = HideOwnLayerFromGameLights();
        var scene = SceneLook.Plain();
        var active = RenderTexture.active;
        try
        {
            _camera.targetTexture = target;
            return (Picture(Color.clear, target, readback), Picture(new Color(1f, 1f, 1f, 0f), target, readback));
        }
        finally
        {
            RenderTexture.active = active;
            _camera.targetTexture = null;
            scene.Restore();
            foreach (var (light, mask) in lights)
            {
                if (light != null)
                    light.cullingMask = mask;
            }
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(readback);
        }
    }

    public void Dispose()
    {
        if (_root != null)
            Object.DestroyImmediate(_root);
    }

    private float[] Picture(Color background, RenderTexture target, Texture2D readback)
    {
        _camera.backgroundColor = background;
        _camera.Render();
        RenderTexture.active = target;
        readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
        var bytes = readback.GetRawTextureData();
        var floats = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, floats, 0, floats.Length * 4);
        return floats;
    }

    private void AddLight(Quaternion rotation, float intensity)
    {
        var holder = new GameObject("Icon light") { hideFlags = HideFlags.HideAndDontSave };
        holder.transform.SetParent(_root.transform, false);
        holder.transform.rotation = rotation;
        var light = holder.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = Color.white;
        light.shadows = LightShadows.None;
        light.cullingMask = 1 << _layer;
    }

    // The game's lights, the sun above all, would light the copy with whatever the time of day is.
    private List<(Light, int)> HideOwnLayerFromGameLights()
    {
        var changed = new List<(Light, int)>();
        int bit = 1 << _layer;
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if ((light.cullingMask & bit) == 0 || light.transform.IsChildOf(_root.transform))
                continue;
            changed.Add((light, light.cullingMask));
            light.cullingMask &= ~bit;
        }
        return changed;
    }

    // A part shows when it and every parent up to the model's root is active; the root itself may be an
    // inactive template.
    private static bool Shown(Transform part, Transform root)
    {
        for (var t = part; t != null && t != root; t = t.parent)
        {
            if (!t.gameObject.activeSelf)
                return false;
        }
        return true;
    }

    // Only the most detailed level of each LOD group is drawn.
    private static HashSet<Renderer> OtherLods(GameObject source)
    {
        var skipped = new HashSet<Renderer>();
        foreach (var group in source.GetComponentsInChildren<LODGroup>(true))
        {
            var lods = group.GetLODs();
            for (int i = 1; i < lods.Length; i++)
            {
                foreach (var renderer in lods[i].renderers)
                {
                    if (renderer != null)
                        skipped.Add(renderer);
                }
            }
        }
        return skipped;
    }

    // Fog and the sky's ambient light would tint the icon by level and time of day.
    private readonly struct SceneLook
    {
        private readonly bool _fog;
        private readonly AmbientMode _mode;
        private readonly SphericalHarmonicsL2 _probe;

        private SceneLook(bool fog, AmbientMode mode, SphericalHarmonicsL2 probe)
        {
            _fog = fog;
            _mode = mode;
            _probe = probe;
        }

        public static SceneLook Plain()
        {
            var saved = new SceneLook(RenderSettings.fog, RenderSettings.ambientMode, RenderSettings.ambientProbe);
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(0.22f, 0.22f, 0.22f));
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Custom;
            RenderSettings.ambientProbe = ambient;
            return saved;
        }

        public void Restore()
        {
            RenderSettings.fog = _fog;
            RenderSettings.ambientMode = _mode;
            RenderSettings.ambientProbe = _probe;
        }
    }
}
