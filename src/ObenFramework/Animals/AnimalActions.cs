// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Npcs;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Animals;

/// <summary>
/// Makes animals in the scene do things through the game's own animal code: sit, sleep (for a while or until
/// woken), wake, run, hunt a target or stop. Hunts and endless sleeps are held by the mod that started them
/// and end when it unloads or the level changes; nothing else is undone. An animal kept asleep by one mod
/// refuses the others' orders until it is woken. Tame and wild is in <see cref="AnimalTames"/>.
/// </summary>
public static class AnimalActions
{
    private sealed class Hold
    {
        public string Owner;
        public Animal Animal;
        public Stats Target;
    }

    private static readonly List<Hold> Hunts = new List<Hold>();
    private static readonly List<Hold> Sleeps = new List<Hold>();
    private static readonly HashSet<string> Tracked = new HashSet<string>();

    /// <summary>Sits down for a while (the game picks how long, 5 to 50 seconds).</summary>
    public static bool Sit(int animalId) => Act(animalId, "Sit", a => a.Sit());

    /// <summary>Lies down to sleep for a minute or so, then gets up by itself.</summary>
    public static bool Sleep(int animalId) => Act(animalId, "Sleep", a => a.Sleep(false));

    /// <summary>
    /// Sleeps until <see cref="Wake"/>, a hit, or the mod's unload. Other mods' orders are refused meanwhile.
    /// </summary>
    public static bool SleepUntilWoken(ObenMod owner, int animalId)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        var animal = Animals.Find(animalId);
        if (animal == null || HeldAsleep(animal, owner.Info.Id))
            return false;
        if (!Run("SleepUntilWoken", () => animal.Sleep(true)))
            return false;
        Sleeps.RemoveAll(s => s.Animal == null || s.Animal == animal);
        Sleeps.Add(new Hold { Owner = owner.Info.Id, Animal = animal });
        Track(owner);
        return true;
    }

    /// <summary>Wakes a sleeping animal. A mod cannot wake one another mod keeps asleep.</summary>
    public static bool Wake(ObenMod owner, int animalId)
    {
        var animal = Animals.Find(animalId);
        if (animal == null || AnimalRecord.CurrentState(animal) != Animal.AnimalState.Sleeping)
            return false;
        if (owner != null ? HeldAsleep(animal, owner.Info.Id) : HeldAsleep(animal, null))
            return false;
        Sleeps.RemoveAll(s => s.Animal == animal);
        return Run("Wake", animal.OnSleepingDone);
    }

    /// <summary>Runs (at its running speed) for <paramref name="seconds"/>.</summary>
    public static bool Run(int animalId, float seconds)
    {
        if (!(seconds > 0f) || float.IsInfinity(seconds))
            return false;
        return Act(animalId, "Run", a => a.Run(seconds));
    }

    /// <summary>
    /// Hunts a target that has stats (the player, an NPC, another animal) until it is caught, the animal
    /// gives up, <see cref="StopHunt"/>, or the mod unloads. A well fed animal may refuse, as in the game.
    /// </summary>
    public static bool Hunt(ObenMod owner, int animalId, Transform target)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        var animal = Animals.Find(animalId);
        var stats = target != null ? target.GetComponentInChildren<Stats>() : null;
        if (animal == null || stats == null || HeldAsleep(animal, owner.Info.Id))
            return false;
        if (!Run("Hunt", () => animal.HuntTarget(target)) || animal.CurrentTarget() != stats)
            return false;
        Hunts.RemoveAll(h => h.Animal == null || h.Animal == animal);
        Hunts.Add(new Hold { Owner = owner.Info.Id, Animal = animal, Target = stats });
        Track(owner);
        return true;
    }

    /// <summary>Calls off a hunt (anyone's, or the game's own) and lets the animal wander again.</summary>
    public static bool StopHunt(int animalId)
    {
        var animal = Animals.Find(animalId);
        if (animal == null || HeldAsleep(animal, null))
            return false;
        Hunts.RemoveAll(h => h.Animal == animal);
        return Run("StopHunt", animal.BreakOffHunt);
    }

    /// <summary>Ends the mod's hunts and wakes the animals it keeps asleep. Returns how many there were.</summary>
    public static int ReleaseAll(ObenMod owner) => owner == null ? 0 : ReleaseOwner(owner.Info.Id);

    internal static void Init(ObenMod framework)
    {
        GameEvents.SceneLoaded.Subscribe(framework, _ =>
        {
            Hunts.Clear();
            Sleeps.Clear();
        });
        framework.Track(Disposable.Create(() =>
        {
            ReleaseOwner(null);
            Tracked.Clear();
        }));
    }

    private static bool Act(int animalId, string what, Action<Animal> act)
    {
        var animal = Animals.Find(animalId);
        if (animal == null || HeldAsleep(animal, null))
            return false;
        return Run(what, () => act(animal));
    }

    // Kept asleep by a mod other than `exceptOwner` (any mod when it is null). Holds of woken animals lapse.
    private static bool HeldAsleep(Animal animal, string exceptOwner)
    {
        if (AnimalRecord.CurrentState(animal) != Animal.AnimalState.Sleeping)
        {
            Sleeps.RemoveAll(s => s.Animal == animal);
            return false;
        }
        foreach (var hold in Sleeps)
        {
            if (hold.Animal == animal && hold.Owner != exceptOwner)
                return true;
        }
        return false;
    }

    private static int ReleaseOwner(string owner)
    {
        int count = 0;
        for (int i = Hunts.Count - 1; i >= 0; i--)
        {
            var hunt = Hunts[i];
            if (owner != null && hunt.Owner != owner)
                continue;
            Hunts.RemoveAt(i);
            count++;
            if (hunt.Animal != null && hunt.Animal.CurrentTarget() == hunt.Target)
                Run("ending a hunt", hunt.Animal.BreakOffHunt);
        }
        for (int i = Sleeps.Count - 1; i >= 0; i--)
        {
            var sleep = Sleeps[i];
            if (owner != null && sleep.Owner != owner)
                continue;
            Sleeps.RemoveAt(i);
            count++;
            if (sleep.Animal != null && AnimalRecord.CurrentState(sleep.Animal) == Animal.AnimalState.Sleeping)
                Run("waking an animal", sleep.Animal.OnSleepingDone);
        }
        return count;
    }

    private static void Track(ObenMod owner)
    {
        if (!Tracked.Add(owner.Info.Id))
            return;
        string id = owner.Info.Id;
        owner.Track(Disposable.Create(() =>
        {
            Tracked.Remove(id);
            ReleaseOwner(id);
        }));
    }

    private static bool Run(string what, Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception e)
        {
            NpcsLog.Failed("AnimalActions." + what, e);
            return false;
        }
    }
}
