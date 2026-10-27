using System.Text.Json.Nodes;
using ObenTool.Bridge;

namespace ObenTool.Mcp;

/// <summary>
/// Tools that forward straight to a DevBridge method. Each entry is one line: tool name, bridge
/// method, description and input schema. Anything not listed is still reachable through bridge_call.
/// </summary>
public static class BridgeTools
{
    public static IEnumerable<McpTool> All(BridgeClient bridge)
    {
        McpTool Forward(string name, string method, string description, Schema schema) =>
            new(name, description, schema, async (args, ct) =>
                ToolResult.Text(BridgeClient.Pretty(await bridge.CallAsync(method, args, ct: ct))));

        yield return new McpTool("bridge_call",
            "Run any DevBridge method in the game. Use bridge_help to list them.",
            Schema.Object()
                .Str("method", "Bridge method name, e.g. game.info", required: true)
                .Obj("params", "Parameters object for the method"),
            async (args, ct) =>
            {
                string method = args["method"]!.GetValue<string>();
                var p = args["params"]?.AsObject().DeepClone().AsObject() ?? new JsonObject();
                return ToolResult.Text(BridgeClient.Pretty(await bridge.CallAsync(method, p, ct: ct)));
            });

        yield return Forward("bridge_help", "help", "List every DevBridge method with its parameters.", Schema.Object());

        yield return Forward("game_info", "game.info",
            "Current scene, loaded mods, fps, time scale, Unity and game versions.", Schema.Object());

        yield return Forward("log_tail", "log.tail",
            "Recent log lines from the game, mods and Unity. Pass the returned 'last' as 'after' to get only new lines.",
            Schema.Object()
                .Int("after", "Only lines after this sequence number")
                .Int("max", "At most this many lines (default 100)")
                .Str("level", "Debug, Info, Warning or Error (default Debug)"));

        yield return Forward("mods_list", "mods.list", "Every mod with its state, version and error.", Schema.Object());

        yield return Forward("mods_reload", "mods.reload",
            "Reload one hot-reload mod (and its dependents), or all of them.",
            Schema.Object().Str("id", "Mod id; omit to reload every hot-reload mod"));

        yield return Forward("scene_find", "scene.find",
            "Find GameObjects by name or component type in the loaded scenes.",
            Schema.Object()
                .Str("name", "Substring of the object name")
                .Str("component", "Component type name, e.g. Door")
                .Int("max", "At most this many results (default 50)"));

        yield return Forward("obj_get", "obj.get",
            "Read a GameObject's components, or one component's fields and properties.",
            Schema.Object()
                .Str("target", "Object path (Root/Child) or #instanceId", required: true)
                .Str("component", "Component type name")
                .Str("member", "Field or property name"));

        yield return Forward("obj_set", "obj.set",
            "Set a field or property on a component.",
            Schema.Object()
                .Str("target", "Object path or #instanceId", required: true)
                .Str("component", "Component type name", required: true)
                .Str("member", "Field or property name", required: true)
                .Any("value", "New value (JSON)", required: true));

        yield return Forward("type_call", "type.call",
            "Call a static method or read a static field/property on any loaded type, e.g. GameController.instance.",
            Schema.Object()
                .Str("type", "Type name, e.g. SaveController", required: true)
                .Str("member", "Method, field or property name", required: true)
                .Arr("args", "Arguments for a method call"));

        yield return Forward("console_exec", "console.exec",
            "Run a command in the game's developer console (the F1 console), e.g. 'add_money 100'.",
            Schema.Object().Str("command", "The console line", required: true));

        yield return new McpTool("screenshot",
            "Capture the game window and return it as an image.",
            Schema.Object().Num("scale", "Resize factor, 0.1 to 1 (default 0.5)"),
            async (args, ct) =>
            {
                var p = new JsonObject { ["scale"] = args["scale"]?.GetValue<double>() ?? 0.5 };
                var result = await bridge.CallAsync("screenshot", p, ct: ct);
                return new ToolResult()
                    .AddImage(result!["data"]!.GetValue<string>(), result["mimeType"]!.GetValue<string>())
                    .AddText($"{result["width"]}x{result["height"]}, scene {result["scene"]}");
            });
    }
}
