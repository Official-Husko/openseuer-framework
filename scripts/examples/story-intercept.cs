// Watches conversations and crimes through the framework's interceptors. Run it with
//   oben run scripts/examples/story-intercept.cs
// then talk to someone or commit a crime (oben call law.commit kind=Vandalism severity=40) and read
// oben logs. Crimes come out at half severity while the script is active; oben run --reset story-intercept
// takes both interceptors away again.
using ObenFramework.Dialogue;
using ObenFramework.Story;
using ScriptRunner;
using UnityEngine;

public static class StoryIntercept
{
    public static object Run(ScriptScope scope)
    {
        Conversations.Intercept(scope, start => scope.Log.Info($"conversation starting: {start.Title}"));
        Law.Intercept(scope, crime =>
        {
            scope.Log.Info($"crime {crime.Kind} {crime.Severity} against {crime.VictimId}, halving it");
            crime.Severity = Mathf.Max(1, crime.Severity / 2);
        });
        Law.Committed.Subscribe(scope, c => scope.Log.Info($"on record: {c.Kind} +{c.Severity}"));
        return "intercepting conversations and crimes";
    }
}
