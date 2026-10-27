using System;
using System.Linq;
using DevBridge.Server;
using ObenKit.Reflection;
using ObenKit.Scenes;
using ObenKit.Values;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DevBridge.Commands;

/// <summary>Generic scene inspection and editing. Targets are "#instanceId" or "Root/Child" paths.</summary>
internal static class ObjectCommands
{
    [BridgeCommand("obj.find", "Find GameObjects by name and/or component.",
        Params = new[] { "name?: substring", "component?: type name", "includeInactive?: bool", "max?: default 50" })]
    private static object Find(JObject p)
    {
        var type = p["component"] != null ? TypeFinder.Require(p.Str("component")) : null;
        var found = SceneQuery.Find(p.Str("name", null), type, p.Bool("includeInactive", false), p.Int("max", 50));
        return new JArray(found.Select(go => Describe(go, components: false)).ToArray());
    }

    [BridgeCommand("obj.tree", "Hierarchy under an object, or the scene roots.", Params = new[] { "target?: path or #id", "depth?: default 2" })]
    private static object Tree(JObject p)
    {
        int depth = p.Int("depth", 2);
        if (p["target"] == null)
            return new JArray(SceneQuery.Roots().Select(r => Node(r.transform, depth)).ToArray());
        return Node(SceneQuery.Resolve(p.Str("target")).transform, depth);
    }

    [BridgeCommand("obj.inspect", "Components of an object, or the members of one component.", Params = new[] { "target", "component?" })]
    private static object Inspect(JObject p)
    {
        var go = SceneQuery.Resolve(p.Str("target"));
        if (p["component"] == null)
            return Describe(go, components: true);

        var component = GetComponent(go, p.Str("component"));
        var members = new JObject();
        foreach (var member in MemberAccess.Inspectable(component.GetType()))
        {
            try { members[member.Name] = JsonValues.ToJson(MemberAccess.Get(component, member), 1); }
            catch (Exception e) { members[member.Name] = "<" + e.GetBaseException().GetType().Name + ">"; }
        }
        return members;
    }

    [BridgeCommand("obj.get", "Read one field or property.", Params = new[] { "target", "component", "member", "depth?: default 2" })]
    private static object Get(JObject p)
    {
        var component = GetComponent(SceneQuery.Resolve(p.Str("target")), p.Str("component"));
        var member = MemberAccess.FindMember(component.GetType(), p.Str("member"));
        return JsonValues.ToJson(MemberAccess.Get(component, member), p.Int("depth", 2));
    }

    [BridgeCommand("obj.set", "Write one field or property.", Params = new[] { "target", "component", "member", "value" })]
    private static object Set(JObject p)
    {
        var component = GetComponent(SceneQuery.Resolve(p.Str("target")), p.Str("component"));
        var member = MemberAccess.FindMember(component.GetType(), p.Str("member"));
        MemberAccess.Set(component, member, JsonValues.FromJson(p["value"], MemberAccess.MemberType(member)));
        return JsonValues.ToJson(MemberAccess.Get(component, member));
    }

    [BridgeCommand("obj.call", "Call a method on a component.", Params = new[] { "target", "component", "method", "args?: array" })]
    private static object Call(JObject p)
    {
        var component = GetComponent(SceneQuery.Resolve(p.Str("target")), p.Str("component"));
        return MethodInvoker.Call(component.GetType(), component, p.Str("method"), p.Array("args"));
    }

    [BridgeCommand("obj.active", "Show or hide an object.", Params = new[] { "target", "value: bool" })]
    private static object Active(JObject p)
    {
        var go = SceneQuery.Resolve(p.Str("target"));
        go.SetActive(p.Bool("value"));
        return go.activeSelf;
    }

    [BridgeCommand("obj.destroy", "Destroy an object.", Params = new[] { "target" })]
    private static object Destroy(JObject p)
    {
        Object.Destroy(SceneQuery.Resolve(p.Str("target")));
        return "destroyed";
    }

    [BridgeCommand("obj.move", "Move an object.", Params = new[] { "target", "x, y, z or pos: [x,y,z]" })]
    private static object Move(JObject p)
    {
        var go = SceneQuery.Resolve(p.Str("target"));
        var position = p.Position() ?? throw new ArgumentException("Give x/y/z or pos.");
        var body = go.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.position = position;
        }
        go.transform.position = position;
        return JsonValues.ToJson(position);
    }

    /// <summary>
    /// Finds a component by type name. Names are matched against the components the object really
    /// has first, because short names are ambiguous (UnityEngine.UI.Image vs UIElements.Image).
    /// </summary>
    internal static Component GetComponent(GameObject go, string typeName)
    {
        foreach (var c in go.GetComponents<Component>())
        {
            if (c != null && (c.GetType().Name == typeName || c.GetType().FullName == typeName))
                return c;
        }

        var type = TypeFinder.Find(typeName);
        var found = type != null ? go.GetComponent(type) : null;
        return found ?? throw new ArgumentException($"{go.name} has no {typeName}.");
    }

    private static JObject Describe(GameObject go, bool components)
    {
        var obj = new JObject
        {
            ["name"] = go.name,
            ["id"] = "#" + go.GetInstanceID(),
            ["path"] = SceneQuery.PathOf(go),
            ["active"] = go.activeInHierarchy,
            ["position"] = JsonValues.ToJson(go.transform.position),
        };
        if (components)
            obj["components"] = new JArray(go.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name).ToArray());
        return obj;
    }

    private static JObject Node(Transform t, int depth)
    {
        var node = new JObject { ["name"] = t.name, ["id"] = "#" + t.gameObject.GetInstanceID(), ["active"] = t.gameObject.activeSelf };
        if (t.childCount == 0)
            return node;
        if (depth <= 0)
        {
            node["children"] = t.childCount;
            return node;
        }
        var children = new JArray();
        foreach (Transform child in t)
            children.Add(Node(child, depth - 1));
        node["children"] = children;
        return node;
    }
}
