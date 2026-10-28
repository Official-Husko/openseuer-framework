// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Saves;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Items;

/// <summary>A snapshot of a spawner: a point in a scene that puts pickups, plants or creatures into the world on a timer.</summary>
public readonly struct SpawnerInfo
{
    public SpawnerInfo(string id, string kind, Vector3 position, bool respawns, float chance, int spawned,
        SpawnSettings.SpawnTime spawnTime, bool customTime, int minSeconds, int maxSeconds, int secondsToSpawn)
    {
        Id = id;
        Kind = kind;
        Position = position;
        Respawns = respawns;
        Chance = chance;
        Spawned = spawned;
        SpawnTime = spawnTime;
        CustomTime = customTime;
        MinSeconds = minSeconds;
        MaxSeconds = maxSeconds;
        SecondsToSpawn = secondsToSpawn;
    }

    public string Id { get; }

    /// <summary>The game class of the spawner.</summary>
    public string Kind { get; }

    public Vector3 Position { get; }

    public bool Respawns { get; }

    /// <summary>Chance from 0 to 1 that a respawn produces anything.</summary>
    public float Chance { get; }

    /// <summary>How many of the objects it spawned still exist.</summary>
    public int Spawned { get; }

    /// <summary>The game's respawn interval class, used unless <see cref="CustomTime"/> is set.</summary>
    public SpawnSettings.SpawnTime SpawnTime { get; }

    public bool CustomTime { get; }

    public int MinSeconds { get; }

    public int MaxSeconds { get; }

    /// <summary>Game seconds counted towards the next respawn.</summary>
    public int SecondsToSpawn { get; }
}

/// <summary>Spawners in the loaded scenes: listing them, changing their respawn settings, spawning now, and hearing them spawn.</summary>
public static class Spawners
{
    private static readonly AccessTools.FieldRef<Spawner, List<GameObject>> Objects =
        AccessTools.FieldRefAccess<Spawner, List<GameObject>>("objects");

    /// <summary>A spawner put new objects into the world. The payload is a fresh snapshot of it.</summary>
    public static readonly WorldEvent<SpawnerInfo> Spawned = new WorldEvent<SpawnerInfo>(nameof(Spawned));

    public static List<SpawnerInfo> All()
    {
        var list = new List<SpawnerInfo>();
        foreach (var spawner in ObjectIds.Active<Spawner>())
            list.Add(Describe(spawner));
        return list;
    }

    public static Spawner Find(string id) => ObjectIds.Find<Spawner>(id);

    public static SpawnerInfo? Get(string id)
    {
        var spawner = Find(id);
        return spawner != null ? Describe(spawner) : (SpawnerInfo?)null;
    }

    /// <summary>Turns respawning on or off and sets its chance (clamped to 0..1). Refused while a save loads.</summary>
    public static bool SetRespawn(string id, bool enabled, float chance)
    {
        var settings = Settings(id);
        if (settings == null)
            return false;
        settings.respawnEnabled = enabled;
        settings.spawnChance = Mathf.Clamp01(chance);
        return true;
    }

    /// <summary>Sets the respawn interval: one of the game's classes, or a custom range in game seconds.</summary>
    public static bool SetRespawnTime(string id, SpawnSettings.SpawnTime time, bool custom = false, int minSeconds = 0, int maxSeconds = 0)
    {
        if (!Enum.IsDefined(typeof(SpawnSettings.SpawnTime), time) || minSeconds < 0 || maxSeconds < minSeconds)
            return false;
        var settings = Settings(id);
        if (settings == null)
            return false;
        settings.spawnTime = time;
        settings.useCustomSpawnTime = custom;
        if (custom)
        {
            settings.minRespawnTime = minSeconds;
            settings.maxRespawnTime = maxSeconds;
        }
        return true;
    }

    /// <summary>Spawns now as if the timer ran out (the chance still applies). True when something spawned.</summary>
    public static bool SpawnNow(string id)
    {
        var spawner = Find(id);
        if (spawner == null || spawner.spawnSettings == null || SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
            return spawner.TrySpawn(ignorePlayer: true);
    }

    public static SpawnerInfo Describe(Spawner spawner)
    {
        var s = spawner.spawnSettings;
        string id = ObjectIds.IdOf(spawner);
        string kind = spawner.GetType().Name;
        if (s == null)
            return new SpawnerInfo(id, kind, spawner.transform.position, false, 0f, Count(spawner), SpawnSettings.SpawnTime.Default, false, 0, 0, 0);
        return new SpawnerInfo(id, kind, spawner.transform.position, s.respawnEnabled, s.spawnChance, Count(spawner), s.spawnTime,
            s.useCustomSpawnTime, s.minRespawnTime, s.maxRespawnTime, s.timetospawn);
    }

    private static SpawnSettings Settings(string id)
    {
        var spawner = Find(id);
        return spawner != null && !SaveGate.IsBusy ? spawner.spawnSettings : null;
    }

    private static int Count(Spawner spawner)
    {
        var objects = Objects(spawner);
        if (objects == null)
            return 0;
        int count = 0;
        foreach (var go in objects)
        {
            if (go != null)
                count++;
        }
        return count;
    }
}
