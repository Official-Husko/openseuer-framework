// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.World;

/// <summary>
/// The timing of a song played on a guitar, without Unity: a list of waits, each the seconds before the next
/// strum. <see cref="Advance"/> is fed the frame time and says how many strums are due, at most a few per
/// frame so a long frame doesn't turn into a burst of noise.
/// </summary>
public sealed class GuitarSong
{
    public const int MaxStrumsPerFrame = 4;
    public const int MaxNotes = 1000;
    public const float MaxWait = 60f;

    private readonly List<float> _waits;
    private int _next;
    private float _untilNext;

    public GuitarSong(IList<float> waits)
    {
        string problem = Check(waits);
        if (problem != null)
            throw new System.ArgumentException(problem, nameof(waits));
        _waits = new List<float>(waits);
        _untilNext = _waits[0];
    }

    /// <summary>Every strum has been played.</summary>
    public bool Done => _next >= _waits.Count;

    /// <summary>Strums played so far.</summary>
    public int Played => _next;

    public int Count => _waits.Count;

    /// <summary>Why a list of waits can't be a song, or null when it can.</summary>
    public static string Check(IList<float> waits)
    {
        if (waits == null || waits.Count == 0)
            return "A song needs at least one note.";
        if (waits.Count > MaxNotes)
            return $"A song has at most {MaxNotes} notes.";
        foreach (float wait in waits)
        {
            if (float.IsNaN(wait) || float.IsInfinity(wait) || wait < 0f || wait > MaxWait)
                return $"Each wait is 0 to {MaxWait} seconds.";
        }
        return null;
    }

    /// <summary>Moves the song on by <paramref name="seconds"/> and returns how many strums are due now.</summary>
    public int Advance(float seconds)
    {
        int due = 0;
        _untilNext -= seconds;
        while (!Done && _untilNext <= 0f && due < MaxStrumsPerFrame)
        {
            due++;
            _next++;
            if (!Done)
                _untilNext += _waits[_next];
        }
        return due;
    }
}
