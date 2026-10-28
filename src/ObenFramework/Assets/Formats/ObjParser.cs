// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ObenFramework.Assets;

/// <summary>
/// Reads Wavefront OBJ text: positions, uvs, normals, faces (triangulated as fans) and
/// <c>usemtl</c> groups. Pure logic with hard limits, so it is safe to run on a worker thread on files
/// from anywhere.
/// </summary>
internal static class ObjParser
{
    public const long MaxFileBytes = 64L * 1024 * 1024;
    public const int MaxVertices = 2000000;
    public const int MaxAttribute = 2097151;
    public const int MaxSubMeshes = 8;
    public const int MaxIndices = MaxVertices * 3;
    private const int MaxLineValues = MaxIndices / 3 + 3;
    private const string TooManyMaterials = "more than 8 materials; the extra ones share the last";
    private static readonly char[] Blanks = { ' ', '\t' };

    private sealed class State
    {
        public bool Flip;
        public readonly List<float> Pos = new List<float>();
        public readonly List<float> Uv = new List<float>();
        public readonly List<float> Nor = new List<float>();
        public readonly List<float> OutPos = new List<float>();
        public readonly List<float> OutUv = new List<float>();
        public readonly List<float> OutNor = new List<float>();
        public readonly Dictionary<ulong, int> Unique = new Dictionary<ulong, int>();
        public readonly ObjData Data = new ObjData();
        public int[] Corners = new int[8];
        public string Material;
        public ObjSubMesh Current;
        public bool FanWarned;
        public int Indices;
        public bool SawUv, SawNoUv, SawNormal, SawNoNormal;
    }

    /// <summary>
    /// Parses OBJ text. With <paramref name="flipX"/> the X axis is negated and the winding reversed,
    /// which turns the right-handed OBJ convention into Unity's left-handed one. Returns null and an
    /// error naming the line when the text can't be used.
    /// </summary>
    public static ObjData Parse(string text, bool flipX, out string error)
    {
        if (text == null)
        {
            error = "no text";
            return null;
        }

        int start = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0;
        var state = new State { Flip = flipX };
        int lineNumber = 0;
        while (start < text.Length)
        {
            int end = text.IndexOf('\n', start);
            if (end < 0)
                end = text.Length;
            lineNumber++;
            string problem = Line(state, text.Substring(start, end - start).Trim());
            if (problem != null)
            {
                error = "line " + lineNumber + ": " + problem;
                return null;
            }
            start = end + 1;
        }

        if (state.Data.SubMeshes.Count == 0)
        {
            error = "no faces";
            return null;
        }

        bool partialUv = state.SawUv && state.SawNoUv;
        bool partialNormal = state.SawNormal && state.SawNoNormal;
        if (partialUv)
            state.Data.Warnings.Add("some corners have a uv and some do not; uvs dropped");
        if (partialNormal)
            state.Data.Warnings.Add("some corners have a normal and some do not; normals dropped and recalculated");
        state.Data.Positions = state.OutPos.ToArray();
        state.Data.Uvs = state.SawUv && !partialUv ? state.OutUv.ToArray() : new float[0];
        state.Data.Normals = state.SawNormal && !partialNormal ? state.OutNor.ToArray() : new float[0];
        error = null;
        return state.Data;
    }

    /// <summary>Reads and parses a file. Returns null with an error for a missing, oversized or invalid file.</summary>
    public static ObjData ParseFile(string path, bool flipX, out string error)
    {
        if (!File.Exists(path))
        {
            error = "file not found: " + path;
            return null;
        }
        try
        {
            if (new FileInfo(path).Length > MaxFileBytes)
            {
                error = "file is larger than " + (MaxFileBytes >> 20) + " MB";
                return null;
            }
            return Parse(File.ReadAllText(path), flipX, out error);
        }
        catch (Exception e)
        {
            error = e.Message;
            return null;
        }
    }

    private static string Line(State s, string line)
    {
        if (line.Length == 0 || line[0] == '#')
            return null;
        if (CountValues(line) > MaxLineValues)
            return "line has more than " + MaxLineValues + " values";

        string[] t = line.Split(Blanks, StringSplitOptions.RemoveEmptyEntries);
        switch (t[0])
        {
            case "v": return Floats(s.Pos, t, 3, "position");
            case "vt": return Floats(s.Uv, t, 2, "uv");
            case "vn": return Floats(s.Nor, t, 3, "normal");
            case "f": return Face(s, t);
            case "usemtl":
                s.Material = line.Substring(t[0].Length).Trim();
                s.Current = null;
                return null;
            case "mtllib":
                if (s.Data.MtlLib == null)
                    s.Data.MtlLib = line.Substring(t[0].Length).Trim();
                return null;
            default:
                return null;
        }
    }

    private static int CountValues(string line)
    {
        int count = 0;
        bool inside = false;
        foreach (char c in line)
        {
            bool separator = c == ' ' || c == '\t';
            if (!separator && !inside)
                count++;
            inside = !separator;
        }
        return count;
    }

    private static string Floats(List<float> into, string[] t, int count, string what)
    {
        if (t.Length < count + 1)
            return what + " needs " + count + " numbers";
        if (into.Count / count >= MaxAttribute)
            return "more than " + MaxAttribute + " " + what + " values";
        for (int i = 1; i <= count; i++)
        {
            if (!float.TryParse(t[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float f) || float.IsNaN(f) || float.IsInfinity(f))
                return "bad number " + t[i];
            into.Add(f);
        }
        return null;
    }

    private static string Face(State s, string[] t)
    {
        int n = t.Length - 1;
        if (n < 3)
            return "face needs at least 3 corners";
        int added = (n - 2) * 3;
        if (s.Indices + added > MaxIndices)
            return "more than " + MaxIndices + " triangle indices";
        s.Indices += added;
        if (n > 4 && !s.FanWarned)
        {
            s.FanWarned = true;
            s.Data.Warnings.Add("faces with more than 4 corners are fan-triangulated; concave faces need Triangulate Faces in the exporter");
        }

        if (s.Corners.Length < n)
            s.Corners = new int[n];
        int[] c = s.Corners;
        for (int i = 0; i < n; i++)
        {
            string problem = Corner(s, t[i + 1], out c[i]);
            if (problem != null)
                return problem;
        }

        if (s.Current == null)
            s.Current = SubMeshFor(s);
        List<int> triangles = s.Current.Triangles;
        for (int i = 1; i + 1 < n; i++)
        {
            triangles.Add(c[0]);
            triangles.Add(s.Flip ? c[i + 1] : c[i]);
            triangles.Add(s.Flip ? c[i] : c[i + 1]);
        }
        return null;
    }

    private static ObjSubMesh SubMeshFor(State s)
    {
        foreach (var m in s.Data.SubMeshes)
        {
            if (m.Material == s.Material)
                return m;
        }
        if (s.Data.SubMeshes.Count < MaxSubMeshes)
        {
            var created = new ObjSubMesh { Material = s.Material };
            s.Data.SubMeshes.Add(created);
            return created;
        }
        if (!s.Data.Warnings.Contains(TooManyMaterials))
            s.Data.Warnings.Add(TooManyMaterials);
        return s.Data.SubMeshes[s.Data.SubMeshes.Count - 1];
    }

    private static string Corner(State s, string token, out int vertex)
    {
        vertex = -1;
        string[] p = token.Split('/');
        if (p.Length > 3)
            return "bad face corner " + token;
        int vt = -1, vn = -1;
        if (!Index(p[0], s.Pos.Count / 3, out int v))
            return "bad vertex index " + token;
        if (p.Length > 1 && p[1].Length > 0 && !Index(p[1], s.Uv.Count / 2, out vt))
            return "bad texture index " + token;
        if (p.Length > 2 && p[2].Length > 0 && !Index(p[2], s.Nor.Count / 3, out vn))
            return "bad normal index " + token;

        if (vt >= 0) s.SawUv = true; else s.SawNoUv = true;
        if (vn >= 0) s.SawNormal = true; else s.SawNoNormal = true;

        // 21 bits each: MaxAttribute is 2^21 - 1, so the +1 for "none" still fits.
        ulong key = ((ulong)(uint)(v + 1) << 42) | ((ulong)(uint)(vt + 1) << 21) | (uint)(vn + 1);
        if (s.Unique.TryGetValue(key, out vertex))
            return null;
        if (s.OutPos.Count / 3 >= MaxVertices)
            return "more than " + MaxVertices + " vertices";
        vertex = s.OutPos.Count / 3;
        s.Unique.Add(key, vertex);
        AddVertex(s, v, vt, vn);
        return null;
    }

    // OBJ indices start at 1; negative ones count back from the end of what was read so far.
    private static bool Index(string text, int count, out int index)
    {
        index = -1;
        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
            return false;
        index = value > 0 ? value - 1 : count + value;
        return value != 0 && index >= 0 && index < count;
    }

    private static void AddVertex(State s, int v, int vt, int vn)
    {
        float x = s.Pos[v * 3];
        s.OutPos.Add(s.Flip ? -x : x);
        s.OutPos.Add(s.Pos[v * 3 + 1]);
        s.OutPos.Add(s.Pos[v * 3 + 2]);
        s.OutUv.Add(vt >= 0 ? s.Uv[vt * 2] : 0f);
        s.OutUv.Add(vt >= 0 ? s.Uv[vt * 2 + 1] : 0f);
        float nx = vn >= 0 ? s.Nor[vn * 3] : 0f;
        s.OutNor.Add(s.Flip ? -nx : nx);
        s.OutNor.Add(vn >= 0 ? s.Nor[vn * 3 + 1] : 0f);
        s.OutNor.Add(vn >= 0 ? s.Nor[vn * 3 + 2] : 0f);
    }
}
