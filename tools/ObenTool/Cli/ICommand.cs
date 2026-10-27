namespace ObenTool.Cli;

/// <summary>A CLI sub-command. Implementations are found by reflection, so adding one needs no registration.</summary>
public interface ICommand
{
    string Name { get; }
    string Usage { get; }
    string Summary { get; }
    Task<int> RunAsync(Args args);
}
