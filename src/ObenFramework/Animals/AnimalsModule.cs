using ObenLoader;

namespace ObenFramework.Animals;

/// <summary>Sets up the animal module: held hunts and sleeps end, and tame choices are saved per save.</summary>
internal static class AnimalsModule
{
    public static void Init(ObenMod framework)
    {
        AnimalActions.Init(framework);
        AnimalTames.Init(framework);
    }
}
