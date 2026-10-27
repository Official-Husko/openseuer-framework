using DevBridge.Server;
using ObenFramework.Reflection;
using ObenFramework.Values;
using Newtonsoft.Json.Linq;

namespace DevBridge.Commands;

/// <summary>Static members of any loaded type, e.g. GameController.instance or ItemDatabase.database.</summary>
internal static class TypeCommands
{
    [BridgeCommand("type.get", "Read a static field or property.", Params = new[] { "type", "member", "depth?: default 2" })]
    private static object Get(JObject p)
    {
        var member = MemberAccess.FindMember(TypeFinder.Require(p.Str("type")), p.Str("member"));
        return JsonValues.ToJson(MemberAccess.Get(null, member), p.Int("depth", 2));
    }

    [BridgeCommand("type.set", "Write a static field or property.", Params = new[] { "type", "member", "value" })]
    private static object Set(JObject p)
    {
        var member = MemberAccess.FindMember(TypeFinder.Require(p.Str("type")), p.Str("member"));
        MemberAccess.Set(null, member, JsonValues.FromJson(p["value"], MemberAccess.MemberType(member)));
        return JsonValues.ToJson(MemberAccess.Get(null, member));
    }

    [BridgeCommand("type.call", "Call a static method.", Params = new[] { "type", "method", "args?: array" })]
    private static object Call(JObject p) =>
        MethodInvoker.Call(TypeFinder.Require(p.Str("type")), null, p.Str("method"), p.Array("args"));
}
