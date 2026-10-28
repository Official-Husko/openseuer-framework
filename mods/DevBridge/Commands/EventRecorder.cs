using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>
/// Keeps the last events of one kind for the *.events commands. Subscribing happens on the first call, so
/// nothing listens (and the framework's hooks stay idle) until someone asks; the subscriptions belong to the
/// bridge and end when it unloads.
/// </summary>
internal sealed class EventRecorder
{
    private const int Capacity = 200;

    private readonly Queue<JObject> _entries = new Queue<JObject>();
    private readonly Action<EventRecorder> _subscribe;
    private bool _started;
    private int _dropped;

    public EventRecorder(Action<EventRecorder> subscribe)
    {
        _subscribe = subscribe;
    }

    /// <summary>Adds an entry with the event name, frame and game-independent time.</summary>
    public void Add(string name, JObject data)
    {
        data["event"] = name;
        data["frame"] = Time.frameCount;
        data["time"] = Math.Round(Time.realtimeSinceStartup, 2);
        _entries.Enqueue(data);
        while (_entries.Count > Capacity)
        {
            _entries.Dequeue();
            _dropped++;
        }
    }

    /// <summary>Starts recording if needed and returns what was recorded since the last clear.</summary>
    public JObject Read(bool clear)
    {
        bool startedNow = !_started;
        if (!_started && DevBridgeMod.Instance != null)
        {
            _subscribe(this);
            _started = true;
        }

        var result = new JObject
        {
            ["recording"] = _started,
            ["startedNow"] = startedNow,
            ["dropped"] = _dropped,
            ["events"] = new JArray(_entries.ToArray()),
        };
        if (clear)
        {
            _entries.Clear();
            _dropped = 0;
        }
        return result;
    }
}
