using ObenFramework.Clock;
using ObenFramework.Dialogue;
using ObenFramework.Economy;
using ObenFramework.Events;
using ObenFramework.Features;
using ObenFramework.Npcs;
using ObenFramework.Player.Look;
using ObenFramework.Player.Survival;
using ObenFramework.Quests;
using ObenFramework.Saves;
using ObenFramework.World;
using ObenLoader;

namespace ObenFramework;

/// <summary>Entry point of the framework mod. Its Harmony patches are applied by the loader.</summary>
public sealed class FrameworkMod : ObenMod
{
    internal static FrameworkMod Instance { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        GameEvents.Init(this);
        SaveState.Init(this);
        SaveGate.Init(this);
        SaveSlots.Init(this);
        ModFeatures.Init(this);
        FeatureCommand.Register(this);
        ModSaves.Init(this);
        GameClock.Init(this);
        DialogueModule.Init(this);
        QuestWatcher.Attach(this);
        WorldModule.Init(this);
        NpcsModule.Init(this);
        PlayerLookWatcher.Init(this);
        SurvivalEvents.Init(this);
        ShopsModule.Init(this);
        Log.Info("Ready.");
    }

    public override void OnUpdate()
    {
        ModFeatures.Update();
        NpcsModule.Update();
    }

    public override void OnUnload() => Instance = null;
}
