using System;
using ObenFramework.Animals;
using ObenFramework.Assets;
using ObenFramework.Building;
using ObenFramework.Clock;
using ObenFramework.Crafting;
using ObenFramework.Dialogue;
using ObenFramework.Economy;
using ObenFramework.Env;
using ObenFramework.Events;
using ObenFramework.Features;
using ObenFramework.Gambling;
using ObenFramework.Items;
using ObenFramework.Modules;
using ObenFramework.NewGames;
using ObenFramework.Npcs;
using ObenFramework.Patching;
using ObenFramework.Player.Look;
using ObenFramework.Player.Survival;
using ObenFramework.Quests;
using ObenFramework.Saves;
using ObenFramework.SceneLook;
using ObenFramework.Transport;
using ObenFramework.Story;
using ObenFramework.Ui;
using ObenFramework.World;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework;

/// <summary>
/// Entry point of the framework mod. It applies its own Harmony patches area by area (mod.json has
/// autoPatch off), then starts each part on its own, so one failing part leaves the rest running.
/// </summary>
public sealed class FrameworkMod : ObenMod
{
    internal static FrameworkMod Instance { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        // Tracked first, so it is undone last: the parts can still log while they clean up.
        Track(Disposable.Create(() =>
        {
            FrameworkModules.Clear();
            Instance = null;
        }));

        ModPatches.ApplyClasses(this);

        Start("events", GameEvents.Init);
        Start("ui", UiModule.Init);
        Start("save state", SaveState.Init);
        Start("save gate", SaveGate.Init);
        Start("save slots", SaveSlots.Init);
        Start("features", ModFeatures.Init, ModFeatures.Update);
        Start("feature command", FeatureCommand.Register);
        Start("mod saves", ModSaves.Init);
        Start("clock", GameClock.Init);
        Start("time control", TimeControl.Init);
        Start("dialogue", DialogueModule.Init);
        Start("quests", QuestWatcher.Attach);
        Start("new games", NewGamesModule.Init);
        Start("story", StoryModule.Init);
        Start("world", WorldModule.Init);
        Start("npcs", NpcsModule.Init, NpcsModule.Update);
        Start("animals", AnimalsModule.Init);
        Start("player look", PlayerLookWatcher.Init);
        Start("survival", SurvivalEvents.Init);
        Start("player", Player.PlayerModule.Init);
        Start("camera", Player.CameraView.Init, Player.CameraView.Update);
        Start("shops", ShopsModule.Init);
        Start("economy", EconomyModule.Init);
        Start("gambling", GamblingModule.Init);
        Start("assets", AssetsModule.Init);
        Start("world extras", WorldExtrasModule.Init);
        Start("scene look", SceneLookModule.Init);
        Start("items", ItemsModule.Init, ItemsModule.Update);
        Start("recipe placeholders", RecipePlaceholders.Init);
        Start("item placeholders", ItemPlaceholders.Init);
        Start("crafting", CraftingModule.Init);
        Start("environment", EnvironmentModule.Init);
        Start("transport", TransportModule.Init, TransportModule.Update);
        Start("custom furniture", CustomFurniture.Init);
        Start("media", MediaModule.Init, MediaModule.Update);
        Start("game check", Diagnostics.GameCheck.Init);
        Start("bug reports", Diagnostics.Reports.BugReports.Init);
        Log.Info(FrameworkModules.Summary());
    }

    public override void OnAllLoaded()
    {
        Diagnostics.GameCheck.StartOnce();
        // After the whole batch, so the mod list in it says which mods really loaded.
        Diagnostics.Reports.BugReports.WriteSnapshot();
    }

    public override void OnUpdate() => FrameworkModules.Update();

    private void Start(string name, Action<ObenMod> init, Action update = null) =>
        FrameworkModules.Start(this, name, init, update);
}
