using System;

namespace ObenCoop.Net.Presence;

/// <summary>
/// The recent samples of one remote player and the maths to draw them smoothly. Samples are placed
/// on the sender's own clock, and the local view runs a fixed delay behind the newest one, so the
/// player is always shown between two real samples (interpolation) unless packets stop coming, when
/// the last one is carried forward along its velocity for a short while (extrapolation).
/// The gap between the two clocks is estimated as the smallest delay seen so far, creeping up slowly
/// so a connection that got slower is followed too. Nothing here allocates.
/// </summary>
public sealed class PresenceTrack
{
    /// <summary>Samples kept. At 10 per second that is 1.6 s of history, far more than the delay needs.</summary>
    public const int Capacity = 16;

    // A jump this big in the sender's clock means it started over (a reload of its mod) or went quiet
    // for a long time; either way the old samples have nothing to do with the new ones.
    private const long RestartGapMs = 10000;

    // How fast the clock gap estimate may grow per sample, in milliseconds.
    private const long OffsetCreepMs = 1;

    private readonly Entry[] _ring = new Entry[Capacity];
    private int _oldest;
    private long _offset;
    private ushort _lastSequence;
    private long _lastTime;

    /// <summary>Samples in the buffer.</summary>
    public int Count { get; private set; }

    /// <summary>Local clock when the newest sample arrived.</summary>
    public long LastArrival { get; private set; }

    /// <summary>Samples dropped because a newer one had already arrived (late or repeated packets).</summary>
    public long Late { get; private set; }

    /// <summary>How often the sender started over (its clock jumped).</summary>
    public long Restarts { get; private set; }

    /// <summary>The newest sample. Only meaningful when <see cref="Count"/> is above zero.</summary>
    public PresenceSample Latest => Count == 0 ? default : _ring[Index(Count - 1)].Sample;

    public void Clear()
    {
        Count = 0;
        _oldest = 0;
    }

    /// <summary>Adds a sample that arrived at local time <paramref name="now"/>. False when it was late or damaged.</summary>
    public bool Add(in PresenceSample sample, long now)
    {
        long time = sample.Time;
        if (Count > 0)
        {
            time = _lastTime + (int)(sample.Time - (uint)_lastTime);
            if (Math.Abs(time - _lastTime) > RestartGapMs)
            {
                Restarts++;
                Clear();
                time = sample.Time;
            }
            else if ((short)(sample.Sequence - _lastSequence) <= 0 && time > _lastTime)
            {
                // A sender's numbers and clock go up together, so a newer time with an older number is the
                // sender counting from 1 again (its mod reloaded), not a late packet.
                Restarts++;
                Clear();
                time = sample.Time;
            }
            else if ((short)(sample.Sequence - _lastSequence) <= 0 || time < _lastTime)
            {
                Late++;
                return false;
            }
        }

        long offset = now - time;
        _offset = Count == 0 ? offset : Math.Min(offset, _offset + OffsetCreepMs);
        _lastSequence = sample.Sequence;
        _lastTime = time;
        LastArrival = now;

        if (Count == Capacity)
        {
            _oldest = (_oldest + 1) % Capacity;
            Count--;
        }
        ref var entry = ref _ring[Index(Count)];
        entry.Time = time;
        entry.Sample = sample;
        Count++;
        return true;
    }

    /// <summary>
    /// Where the player should be shown at local time <paramref name="now"/>, <paramref name="delayMs"/>
    /// behind the newest sample. False when there are no samples.
    /// </summary>
    public bool Sample(long now, int delayMs, int maxExtrapolationMs, out PresencePose pose)
    {
        pose = default;
        if (Count == 0)
            return false;

        long render = now - _offset - delayMs;
        ref readonly var newest = ref _ring[Index(Count - 1)];
        pose.AgeMs = now - LastArrival;

        if (render >= newest.Time)
        {
            long ahead = Math.Min(render - newest.Time, maxExtrapolationMs);
            Fill(ref pose, newest.Sample);
            if (ahead > 0 && (newest.Sample.Flags & PresenceFlags.Moving) != 0)
            {
                pose.Position = newest.Sample.Position + newest.Sample.Velocity * (ahead / 1000f);
                pose.Extrapolated = true;
            }
            return true;
        }

        for (int i = Count - 2; i >= 0; i--)
        {
            ref readonly var a = ref _ring[Index(i)];
            if (a.Time > render)
                continue;
            ref readonly var b = ref _ring[Index(i + 1)];
            Blend(ref pose, a, b, render);
            return true;
        }

        // Older than anything kept: show the oldest sample.
        Fill(ref pose, _ring[Index(0)].Sample);
        return true;
    }

    private static void Blend(ref PresencePose pose, in Entry a, in Entry b, long render)
    {
        bool jump = (b.Sample.Flags & PresenceFlags.Teleported) != 0 || !string.Equals(a.Sample.Area, b.Sample.Area, StringComparison.Ordinal);
        if (jump)
        {
            // No sliding through walls or across levels: stay at the old spot until the jump happened.
            Fill(ref pose, a.Sample);
            return;
        }

        long span = b.Time - a.Time;
        float t = span <= 0 ? 1f : (float)(render - a.Time) / span;
        var nearer = t < 0.5f ? a.Sample : b.Sample;
        pose.Area = nearer.Area;
        pose.Flags = nearer.Flags;
        pose.Position = PresenceVector.Lerp(a.Sample.Position, b.Sample.Position, t);
        pose.Velocity = PresenceVector.Lerp(a.Sample.Velocity, b.Sample.Velocity, t);
        pose.Yaw = LerpAngle(a.Sample.Yaw, b.Sample.Yaw, t);
    }

    private static void Fill(ref PresencePose pose, in PresenceSample sample)
    {
        pose.Area = sample.Area;
        pose.Flags = sample.Flags;
        pose.Position = sample.Position;
        pose.Velocity = sample.Velocity;
        pose.Yaw = sample.Yaw;
    }

    /// <summary>Turns the short way round from <paramref name="from"/> to <paramref name="to"/>. Result in 0..360.</summary>
    public static float LerpAngle(float from, float to, float t)
    {
        float angle = (from + DeltaAngle(from, to) * t) % 360f;
        return angle < 0f ? angle + 360f : angle;
    }

    /// <summary>The shortest signed turn from one heading to another, -180 to 180 degrees.</summary>
    public static float DeltaAngle(float from, float to)
    {
        float delta = (to - from) % 360f;
        if (delta > 180f)
            delta -= 360f;
        else if (delta < -180f)
            delta += 360f;
        return delta;
    }

    private int Index(int i) => (_oldest + i) % Capacity;

    private struct Entry
    {
        public long Time;
        public PresenceSample Sample;
    }
}
