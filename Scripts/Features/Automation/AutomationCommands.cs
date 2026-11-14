using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace CardCleaner.Scripts.Features.Automation;

/// <summary>
///     The command table of the automation agent, plus the frame waits every command shares. Commands run one at a
///     time on the main thread; a handler awaits frames through <see cref="NextFrame" /> or
///     <see cref="NextPhysicsFrame" />, which keep firing while the tree is paused.
/// </summary>
public sealed class AutomationCommands
{
    private readonly Dictionary<string, AutomationCommand> _commands = new(StringComparer.Ordinal);

    public AutomationCommands(Node host)
    {
        Host = host;
        Add(new AutomationCommand("help", "lists every command with its arguments",
            _ => Task.FromResult<JsonNode?>(Help())));
        CoreCommands.Register(this);
        InputCommands.Register(this);
        InspectCommands.Register(this);
        MotionCommands.Register(this);
        CaptureCommands.Register(this);
    }

    public Node Host { get; }

    /// <summary>The run's one mouse, shared by the input commands and mouse look.</summary>
    public VirtualPointer Mouse { get; } = new();

    public SceneTree Tree => Host.GetTree();

    public IEnumerable<string> Names => _commands.Keys;

    public void Add(AutomationCommand command) => _commands.Add(command.Name, command);

    public async Task<string> Execute(AutomationRequest request)
    {
        if (!_commands.TryGetValue(request.Command, out var command))
            return AutomationProtocol.Failure(request.Id, $"unknown command \"{request.Command}\"; try \"help\"");
        try
        {
            return AutomationProtocol.Success(request.Id, await command.Run(request.Args));
        }
        catch (AutomationException e)
        {
            return AutomationProtocol.Failure(request.Id, e.Message);
        }
        catch (Exception e)
        {
            // Any failure inside one command is that command's reply; the agent keeps serving the next one.
            return AutomationProtocol.Failure(request.Id, $"{request.Command} failed: {e.GetType().Name}: {e.Message}");
        }
    }

    public async Task NextFrame() => await Host.ToSignal(Tree, SceneTree.SignalName.ProcessFrame);

    public async Task NextPhysicsFrame() => await Host.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);

    public async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
            await NextFrame();
    }

    private JsonNode Help()
    {
        var help = new JsonObject();
        foreach (var command in _commands.Values.OrderBy(c => c.Name, StringComparer.Ordinal))
            help[command.Name] = command.Usage;
        return help;
    }
}
