using ObenTool.Changelog;
using ObenTool.Cli;

namespace ObenTool.Commands;

public sealed class ChangelogCommand : ICommand
{
    public string Name => "changelog";
    public string Usage => "changelog [--write] [--out FILE] [--release X.Y.Z [--notes]] [--since REF]";
    public string Summary => "Print changelog.json built from Changelog-* commit trailers (--write saves it), or release notes with --notes or --since";

    public Task<int> RunAsync(Args args)
    {
        string repo = Git.RepoRoot(Directory.GetCurrentDirectory());

        // --since prints notes for the commits after REF (an experimental release's previous tag), nothing when
        // none of them has a line for players.
        string? since = args.Get("since");
        if (args.Has("since"))
        {
            if (string.IsNullOrEmpty(since))
                throw new ArgumentException("--since needs a tag or commit.");
            var entry = ChangelogBuilder.Entry(repo, new ReleaseRange("since " + since, since + "..HEAD", false, null));
            if (entry != null)
                Console.Write(ReleaseNotes.Markdown(entry));
            return Task.FromResult(0);
        }

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
