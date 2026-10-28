// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;

namespace ObenFramework.Player.Look;

/// <summary>One body or face slider: its range and how to move it between a <see cref="LookData"/>, an Appearance and a Character.</summary>
internal sealed class LookDial
{
    public LookDial(string name, float min, float max, Func<LookData, float> get, Action<LookData, float> set,
        Func<Appearance, float> read, Action<Character, float> write)
    {
        Name = name;
        Min = min;
        Max = max;
        Get = get;
        Set = set;
        Read = read;
        Write = write;
    }

    public string Name { get; }
    public float Min { get; }
    public float Max { get; }
    public Func<LookData, float> Get { get; }
    public Action<LookData, float> Set { get; }

    /// <summary>The live value on an Appearance.</summary>
    public Func<Appearance, float> Read { get; }

    /// <summary>Writes the value into a Character asset (what the Appearance reads on LoadCharacter).</summary>
    public Action<Character, float> Write { get; }

    public float Clamp(float value) => float.IsNaN(value) ? Min : Math.Min(Math.Max(value, Min), Max);
}

/// <summary>Every slider in a fixed order. The binary form relies on this order, so only ever append.</summary>
internal static class LookDials
{
    public static readonly LookDial[] All =
    {
        new LookDial("height", 0f, 1f, l => l.height, (l, v) => l.height = v, a => a.height, (c, v) => c.Height = v),
        new LookDial("shape", -1f, 1f, l => l.shape, (l, v) => l.shape = v, a => a.shape, (c, v) => c.Weight = v),
        new LookDial("gender", 0f, 1f, l => l.gender, (l, v) => l.gender = v, a => a.gender, (c, v) => c.Gender = v),
        new LookDial("headSize", 0f, 1f, l => l.headSize, (l, v) => l.headSize = v, a => a.headSize, (c, v) => c.HeadSize = v),
        new LookDial("faceSize", 0f, 1f, l => l.faceSize, (l, v) => l.faceSize = v, a => a.faceSize, (c, v) => c.FaceSize = v),
        new LookDial("neckLength", 0f, 1f, l => l.neckLength, (l, v) => l.neckLength = v, a => a.neckLength, (c, v) => c.NeckLength = v),
        new LookDial("shoulderWidth", 0f, 1f, l => l.shoulderWidth, (l, v) => l.shoulderWidth = v, a => a.shoulderWidth, (c, v) => c.ShoulderWidth = v),
        new LookDial("breastSize", 0f, 1f, l => l.breastSize, (l, v) => l.breastSize = v, a => a.breastSize, (c, v) => c.BreastSize = v),
        new LookDial("hipWidth", 0f, 1f, l => l.hipWidth, (l, v) => l.hipWidth = v, a => a.hipWidth, (c, v) => c.HipWidth = v),
        new LookDial("handSize", 0f, 1f, l => l.handSize, (l, v) => l.handSize = v, a => a.handSize, (c, v) => c.HandSize = v),
        new LookDial("footSize", 0f, 1f, l => l.footSize, (l, v) => l.footSize = v, a => a.footSize, (c, v) => c.FootSize = v),
        new LookDial("lipThickness", 0f, 1f, l => l.lipThickness, (l, v) => l.lipThickness = v, a => a.lipThickness, (c, v) => c.LipThickness = v),
        new LookDial("jawPosition", 0f, 1f, l => l.jawPosition, (l, v) => l.jawPosition = v, a => a.jawPosition, (c, v) => c.JawPosition = v),
        new LookDial("chinPosition", 0f, 1f, l => l.chinPosition, (l, v) => l.chinPosition = v, a => a.chinPosition, (c, v) => c.ChinPosition = v),
        new LookDial("eyeSize", 0f, 1f, l => l.eyeSize, (l, v) => l.eyeSize = v, a => a.eyeSize, (c, v) => c.EyeSize = v),
        new LookDial("eyeDistance", 0f, 1f, l => l.eyeDistance, (l, v) => l.eyeDistance = v, a => a.eyeDistance, (c, v) => c.EyeDistance = v),
        new LookDial("eyePosition", 0f, 1f, l => l.eyePosition, (l, v) => l.eyePosition = v, a => a.eyePosition, (c, v) => c.EyePosition = v),
        new LookDial("eyeDepth", 0f, 1f, l => l.eyeDepth, (l, v) => l.eyeDepth = v, a => a.eyeDepth, (c, v) => c.EyeDepth = v),
        new LookDial("earSize", 0f, 1f, l => l.earSize, (l, v) => l.earSize = v, a => a.earSize, (c, v) => c.EarSize = v),
        new LookDial("earRotation", 0f, 1f, l => l.earRotation, (l, v) => l.earRotation = v, a => a.earRotation, (c, v) => c.EarRotation = v),
        new LookDial("noseRootRotation", 0f, 1f, l => l.noseRootRotation, (l, v) => l.noseRootRotation = v, a => a.noseRootRotation, (c, v) => c.NoseRootRotation = v),
        new LookDial("noseTipRotation", 0f, 1f, l => l.noseTipRotation, (l, v) => l.noseTipRotation = v, a => a.noseTipRotation, (c, v) => c.NoseTipRotation = v),
        new LookDial("noseWidth", 0f, 1f, l => l.noseWidth, (l, v) => l.noseWidth = v, a => a.noseWidth, (c, v) => c.NoseWidth = v),
    };
}
