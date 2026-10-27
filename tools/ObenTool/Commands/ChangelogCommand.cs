using ObenTool.Changelog;
using ObenTool.Cli;

namespace ObenTool.Commands;

public sealed class ChangelogCommand : ICommand
{
    public string Name => "changelog";
    public string Usage => "changelog [--write] [--out FILE] [--release X.Y.Z [--notes]]";
    public string Summary => "Print changelog.json built from Changelog-* commit trailers (--write saves it), or release notes with --notes";

    public Task<int> RunAsync(Args args)
    {
        string repo = Git.RepoRoot(Directory.GetCurrentDirectory());

        // --release treats the commits since the last tag as that version, released today.
        // "--notes X.Y.Z" is the short form of "--release X.Y.Z --notes".
        string? notesVersion = args.Get("notes");
        string? release = args.Get("release") ?? notesVersion;
        if (args.Has("notes") && release == null)
            throw new ArgumentException("--notes needs a version: --release X.Y.Z --notes.");

        var changelog = ChangelogBuilder.Build(repo, release);

        if (args.Has("notes"))
        {
            Console.Write(ReleaseNotes.Markdown(changelog, release!));
            return Task.FromResult(0);
        }

        string json = JsonOut.Format(changelog) + "\n";
        if (!args.Has("write"))
        {
            Console.Write(json);
            return Task.FromResult(0);
        }

        string path = args.Get("out") ?? Path.Combine(repo, "changelog.json");
        File.WriteAllText(path, json);
        Out.Ok($"Wrote {path}");
        return Task.FromResult(0);
    }
}
