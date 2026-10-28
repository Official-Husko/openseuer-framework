using ObenLoader;

namespace ObenFramework.Ui;

/// <summary>Sets up <see cref="UiState"/> and <see cref="InputCapture"/> for the framework's lifetime.</summary>
internal static class UiModule
{
    public static void Init(ObenMod framework)
    {
        UiState.Init(framework);
        InputCapture.Init(framework);
    }
}
