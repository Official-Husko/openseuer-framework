namespace ObenFramework.Saves;

/// <summary>What is actually in a mod save file: the mod's object plus a format number for the file layout.</summary>
internal sealed class SaveEnvelope<T>
{
    public const int CurrentFormat = 1;

    public int Format = CurrentFormat;
    public T Data;
}
