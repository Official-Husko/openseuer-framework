using ObenUIKit.Menu.Settings;

namespace GameTweaks;

/// <summary>
/// The values the tweaks use, in Mods/GameTweaks/config.json and on the Settings page of the MODS menu.
/// A value only matters while its tweak is switched on (on the Mods page or with the features command);
/// the defaults are what a tweak does when it is first switched on. Values outside a slider's range in the
/// file are put back in range at start.
/// </summary>
public sealed class TweaksConfig
{
    [Setting("Hunger", Group = "Needs", Description = "0.5 makes you get hungry half as fast, 0 stops it.", Min = 0f, Max = 5f, Step = 0.05f)]
    public float Hunger = 0.5f;

    [Setting("Thirst", Group = "Needs", Description = "1 is the game's own rate.", Min = 0f, Max = 5f, Step = 0.05f)]
    public float Thirst = 0.5f;

    [Setting("Hygiene", Group = "Needs", Description = "How fast you get dirty.", Min = 0f, Max = 5f, Step = 0.05f)]
    public float Hygiene = 0.5f;

    [Setting("Alcohol craving", Group = "Needs", Min = 0f, Max = 5f, Step = 0.05f)]
    public float AlcoholCraving = 0.5f;

    [Setting("Mushroom craving", Group = "Needs", Min = 0f, Max = 5f, Step = 0.05f)]
    public float MushroomCraving = 0.5f;

    [Setting("Smoking craving", Group = "Needs", Min = 0f, Max = 5f, Step = 0.05f)]
    public float SmokingCraving = 0.5f;

    [Setting("Gambling craving", Group = "Needs", Description = "Used by the needs tweak, like the six above.", Min = 0f, Max = 5f, Step = 0.05f)]
    public float GamblingCraving = 0.5f;

    [Setting("Stack size", Group = "Items", Description = "Used by the stack tweak. 2 doubles how many of an item fit in one slot; liquid containers hold more per slot too.", Min = 0.25f, Max = 20f, Step = 0.25f)]
    public float StackSize = 2f;

    [Setting("Spoilage", Group = "Items", Description = "Used by the spoilage tweak. 0.5 makes food last twice as long, 0 stops spoiling.", Min = 0f, Max = 5f, Step = 0.05f)]
    public float Spoilage = 0.5f;

    [Setting("Crafting speed", Group = "Crafting", Description = "Used by the crafting tweak. 2 crafts twice as fast at every station and workbench (a craft still takes a second at least).", Min = 0.25f, Max = 20f, Step = 0.25f)]
    public float CraftingSpeed = 2f;

    [Setting("Dishwasher speed", Group = "Crafting", Description = "Used by the dishwasher tweak, on top of the crafting speed.", Min = 0.25f, Max = 20f, Step = 0.25f)]
    public float DishwasherSpeed = 2f;

    [Setting("Renovation cost", Group = "Renovations", Description = "Used by the renovation cost tweak. 0.5 halves both payments.", Min = 0f, Max = 5f, Step = 0.05f)]
    public float RenovationCost = 0.5f;

    [Setting("Renovation time", Group = "Renovations", Description = "Used by the renovation time tweak. 0.5 halves the build time of new orders.", Min = 0.05f, Max = 5f, Step = 0.05f)]
    public float RenovationTime = 0.5f;

    [Setting("Instant renovations", Group = "Renovations", Description = "Used by the renovation time tweak: new orders are done at once (the story's own upgrades keep their wait).")]
    public bool InstantRenovations;

    [Setting("Time speed", Group = "Time", Description = "Used by the day length tweak. 0.5 makes days last twice as long, 2 makes them pass twice as fast.", Min = 0.1f, Max = 10f, Step = 0.05f)]
    public float TimeSpeed = 0.5f;

    [Setting("Third person distance", Group = "Camera", Description = "Used by the camera tweak: how far behind you the camera goes, in metres.", Min = 0.5f, Max = 4f, Step = 0.1f)]
    public float CameraDistance = 2.5f;

    [Setting("Scroll to zoom", Group = "Camera", Description = "Used by the camera tweak: the mouse wheel moves the camera in and out, into third person and back.")]
    public bool ScrollZoom;
}
