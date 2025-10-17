using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Hkmp.Logging;

namespace Hkmp.Game.Command;

/// <summary>
/// High-performance command dispatcher with caching, validation, and async support.
/// </summary>
internal sealed class FastCommandDispatcher {
    private readonly ConcurrentDictionary<string, CommandDescriptor> _commands;
    private readonly ConcurrentDictionary<string, string> _aliases;
    private readonly Dictionary<string, List<string>> _helpCategories;
    private readonly object _lock = new();

    private sealed class CommandDescriptor {
        public string Name { get; }
        public string Description { get; }
        public string Category { get; }
        public Func<string[], Task<bool>> AsyncHandler { get; }
        public Action<string[]> SyncHandler { get; }
        public int MinArgs { get; }
        public int MaxArgs { get; }
        public bool RequiresAuth { get; }
        public string Usage { get; }

        public CommandDescriptor(
            string name,
            string description,
            string category,
            Func<string[], Task<bool>> asyncHandler,
            Action<string[]> syncHandler,
            int minArgs,
            int maxArgs,
            bool requiresAuth,
            string usage) {
            
            Name = name;
            Description = description;
            Category = category;
            AsyncHandler = asyncHandler;
            SyncHandler = syncHandler;
            MinArgs = minArgs;
            MaxArgs = maxArgs;
            RequiresAuth = requiresAuth;
            Usage = usage;
        }
    }

    public FastCommandDispatcher() {
        _commands = new ConcurrentDictionary<string, CommandDescriptor>(StringComparer.OrdinalIgnoreCase);
        _aliases = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _helpCategories = new Dictionary<string, List<string>>();
    }

    public void RegisterCommand(
        string name,
        string description,
        Action<string[]> handler,
        string category = "General",
        int minArgs = 0,
        int maxArgs = int.MaxValue,
        bool requiresAuth = false,
        string usage = null,
        params string[] aliases) {

        var descriptor = new CommandDescriptor(
            name,
            description,
            category,
            null,
            handler,
            minArgs,
            maxArgs,
            requiresAuth,
            usage ?? $"{name} [args]"
        );

        if (!_commands.TryAdd(name.ToLowerInvariant(), descriptor)) {
            Logger.Warn($"Command '{name}' already registered");
            return;
        }

        lock (_lock) {
            if (!_helpCategories.ContainsKey(category)) {
                _helpCategories[category] = new List<string>();
            }
            _helpCategories[category].Add(name);
        }

        foreach (var alias in aliases) {
            _aliases.TryAdd(alias.ToLowerInvariant(), name.ToLowerInvariant());
        }
    }

    public void RegisterAsyncCommand(
        string name,
        string description,
        Func<string[], Task<bool>> handler,
        string category = "General",
        int minArgs = 0,
        int maxArgs = int.MaxValue,
        bool requiresAuth = false,
        string usage = null,
        params string[] aliases) {

        var descriptor = new CommandDescriptor(
            name,
            description,
            category,
            handler,
            null,
            minArgs,
            maxArgs,
            requiresAuth,
            usage ?? $"{name} [args]"
        );

        if (!_commands.TryAdd(name.ToLowerInvariant(), descriptor)) {
            Logger.Warn($"Command '{name}' already registered");
            return;
        }

        lock (_lock) {
            if (!_helpCategories.ContainsKey(category)) {
                _helpCategories[category] = new List<string>();
            }
            _helpCategories[category].Add(name);
        }

        foreach (var alias in aliases) {
            _aliases.TryAdd(alias.ToLowerInvariant(), name.ToLowerInvariant());
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ResolveCommand(string input) {
        var key = input.ToLowerInvariant();
        return _aliases.TryGetValue(key, out var commandName) ? commandName : key;
    }

    public async Task<bool> ExecuteAsync(string commandLine, bool hasAuth = true) {
        if (string.IsNullOrWhiteSpace(commandLine)) return false;

        var parts = ParseCommandLine(commandLine);
        if (parts.Length == 0) return false;

        var commandName = ResolveCommand(parts[0]);
        if (!_commands.TryGetValue(commandName, out var descriptor)) {
            Logger.Warn($"Unknown command: {parts[0]}");
            return false;
        }

        if (descriptor.RequiresAuth && !hasAuth) {
            Logger.Warn($"Command '{commandName}' requires authorization");
            return false;
        }

        var args = new string[parts.Length - 1];
        Array.Copy(parts, 1, args, 0, args.Length);

        if (args.Length < descriptor.MinArgs) {
            Logger.Warn($"Too few arguments. Usage: {descriptor.Usage}");
            return false;
        }

        if (args.Length > descriptor.MaxArgs) {
            Logger.Warn($"Too many arguments. Usage: {descriptor.Usage}");
            return false;
        }

        try {
            if (descriptor.AsyncHandler != null) {
                return await descriptor.AsyncHandler(args);
            }
            
            descriptor.SyncHandler?.Invoke(args);
            return true;
        } catch (Exception ex) {
            Logger.Error($"Error executing command '{commandName}': {ex}");
            return false;
        }
    }

    public bool Execute(string commandLine, bool hasAuth = true) {
        return ExecuteAsync(commandLine, hasAuth).GetAwaiter().GetResult();
    }

    private static string[] ParseCommandLine(string commandLine) {
        var parts = new List<string>();
        var inQuotes = false;
        var current = "";

        for (var i = 0; i < commandLine.Length; i++) {
            var c = commandLine[i];

            if (c == '"') {
                inQuotes = !inQuotes;
            } else if (char.IsWhiteSpace(c) && !inQuotes) {
                if (current.Length > 0) {
                    parts.Add(current);
                    current = "";
                }
            } else {
                current += c;
            }
        }

        if (current.Length > 0) {
            parts.Add(current);
        }

        return parts.ToArray();
    }

    public string GetHelp(string category = null) {
        var help = "Available Commands:\n";
        help += "==================\n\n";

        lock (_lock) {
            var categories = category != null && _helpCategories.ContainsKey(category)
                ? new[] { category }
                : _helpCategories.Keys;

            foreach (var cat in categories) {
                if (!_helpCategories.TryGetValue(cat, out var commands)) continue;

                help += $"{cat}:\n";
                foreach (var cmdName in commands) {
                    if (_commands.TryGetValue(cmdName.ToLowerInvariant(), out var descriptor)) {
                        help += $"  {descriptor.Name,-20} - {descriptor.Description}\n";
                        help += $"    Usage: {descriptor.Usage}\n";
                    }
                }
                help += "\n";
            }
        }

        return help;
    }

    public bool UnregisterCommand(string name) {
        var key = name.ToLowerInvariant();
        
        if (!_commands.TryRemove(key, out var descriptor)) {
            return false;
        }

        lock (_lock) {
            if (_helpCategories.TryGetValue(descriptor.Category, out var commands)) {
                commands.Remove(descriptor.Name);
            }
        }

        var aliasesToRemove = new List<string>();
        foreach (var kvp in _aliases) {
            if (kvp.Value == key) {
                aliasesToRemove.Add(kvp.Key);
            }
        }

        foreach (var alias in aliasesToRemove) {
            _aliases.TryRemove(alias, out _);
        }

        return true;
    }

    public void Clear() {
        _commands.Clear();
        _aliases.Clear();
        lock (_lock) {
            _helpCategories.Clear();
        }
    }

    public IReadOnlyList<string> GetCommandNames() {
        return new List<string>(_commands.Keys);
    }
}
