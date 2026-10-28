// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Events;
using ObenFramework.Npcs;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Mods;

namespace ObenFramework.Animals;

/// <summary>
/// Tame and wild. A tame animal does not bite the player and can follow them. The game stores the flag in
/// the animal's stats but does not put it back on the animal of a scene when the level loads, so a choice
/// made here is kept in the framework's per-save file (<c>mods/ObenFramework.animals.json</c>) and applied
/// again after every load of that scene, as long as the mod that made it is installed. Animals the game
/// never saves (no save id) are tamed for the session only.
/// </summary>
public static class AnimalTames
{
    private static ModSaveData<AnimalTameFile> _data;

    private static AnimalTameFile File => _data != null ? _data.Value : Empty;

    private static readonly AnimalTameFile Empty = new AnimalTameFile();

    /// <summary>
    /// Tames or unleashes an animal for good (until a hit turns it wild, as in the game). False when the
    /// animal is gone, has no stats, or the mod already keeps 1024 choices in this save.
    /// </summary>
    public static bool SetTame(ObenMod owner, int animalId, bool tame)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        var animal = Animals.Find(animalId);
        var stats = animal != null ? animal.GetComponent<AnimalStats>() : null;
        if (stats == null || stats.animalData == null)
            return false;

        string scene = animal.gameObject.scene.name;
        if (!string.IsNullOrEmpty(stats.GUID))
        {
            if (Holders(Animals.Scan(), scene, stats.GUID) > 1)
                NpcsLog.Warn($"Two animals in {scene} share the save id {stats.GUID}; the tame choice lasts for this session only.");
            else if (!File.Set(owner.Info.Id, scene, stats.GUID, tame))
                return false;
        }
        Apply(animal, stats, tame);
        return true;
    }

    /// <summary>Forgets every tame choice the mod made in this save. The animals stay as they are now. Returns how many there were.</summary>
    public static int Forget(ObenMod owner) => owner == null || SaveGate.IsBusy ? 0 : File.Forget(owner.Info.Id);

    internal static void Init(ObenMod framework)
    {
        _data = ModSaves.Register<AnimalTameFile>(framework, "animals");
        Clean(_data.Value);
        _data.DataLoaded.Subscribe(framework, Clean);
        GameEvents.WhenWorldReady(framework, Restore);
        framework.Track(ObenLoader.Runtime.Disposable.Create(() => _data = null));
    }

    private static void Restore(string scene)
    {
        if (File.Entries.Count == 0)
            return;
        try
        {
            var all = Animals.Scan();
            foreach (var animal in all)
            {
                var stats = animal.GetComponent<AnimalStats>();
                if (stats == null || stats.animalData == null || string.IsNullOrEmpty(stats.GUID) || animal.gameObject.scene.name != scene)
                    continue;
                var tame = File.For(scene, stats.GUID, ModRegistry.IsLoaded);
                if (tame.HasValue && Holders(all, scene, stats.GUID) == 1)
                    Apply(animal, stats, tame.Value);
            }
        }
        catch (Exception e)
        {
            NpcsLog.Failed("Restoring tame animals", e);
        }
    }

    private static void Apply(Animal animal, AnimalStats stats, bool tame)
    {
        animal.ChangeTameStatus(tame);
        stats.animalData.tame = tame;
        if (!tame && AnimalController.instance != null)
            AnimalController.instance.followingAnimals.Remove(animal);
    }

    private static int Holders(Animal[] all, string scene, string guid)
    {
        int count = 0;
        foreach (var animal in all)
        {
            var stats = animal.GetComponent<AnimalStats>();
            if (stats != null && stats.GUID == guid && animal.gameObject.scene.name == scene)
                count++;
        }
        return count;
    }

    private static void Clean(AnimalTameFile file)
    {
        var clean = AnimalTameFile.Clean(file);
        file.Entries.Clear();
        file.Entries.AddRange(clean.Entries);
    }
}
