using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ObenLoader;
using ObenLoader.Runtime;
using Newtonsoft.Json.Linq;

namespace DevBridge.Server;

/// <summary>Command table. Built-in commands come from [BridgeCommand] methods; other mods can add more.</summary>
public sealed class CommandRouter
{
    public sealed class Command
    {
        public string Name;
        public string Description;
        public string[] Params;
        public Func<JObject, object> Handler;
    }

    private readonly Dictionary<string, Command> _commands = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<Command> All => _commands.Values.OrderBy(c => c.Name);

    public void AddFromAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var attr = method.GetCustomAttribute<BridgeCommandAttribute>();
                if (attr == null)
                    continue;
                var handler = (Func<JObject, object>)Delegate.CreateDelegate(typeof(Func<JObject, object>), method);
                Add(attr.Name, attr.Description, attr.Params, handler);
            }
        }
    }

    public void Add(string name, string description, string[] parameters, Func<JObject, object> handler)
    {
        _commands[name] = new Command { Name = name, Description = description, Params = parameters ?? new string[0], Handler = handler };
    }

    /// <summary>Adds a command owned by another mod; it disappears when that mod unloads.</summary>
    public void Add(ObenMod owner, string name, string description, string[] parameters, Func<JObject, object> handler)
    {
        Add(name, description, parameters, handler);
        owner.Track(Disposable.Create(() => _commands.Remove(name)));
    }

    /// <summary>Runs on the main thread. Returns the result, or a <see cref="Deferred"/> still in flight.</summary>
    public object Execute(string name, JObject parameters)
    {
        if (!_commands.TryGetValue(name, out var command))
            throw new ArgumentException($"Unknown method '{name}'. Call 'help' for the list.");
        return command.Handler(parameters ?? new JObject());
    }
}
