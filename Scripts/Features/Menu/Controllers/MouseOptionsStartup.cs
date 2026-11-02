using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Menu.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Menu.Controllers;

/// <summary>
///     Applies the saved mouse options to the scene's <see cref="IGameSettings" /> when the scene starts. The engine-global
///     options are applied by the main menu, so this node only carries the mouse part into a running scene.
/// </summary>
public partial class MouseOptionsStartup : Node, IDependent
{
    private IServiceResolver? _services;

    public void Inject(IServiceResolver services)
    {
        _services = services;
    }

    public override void _Ready()
    {
        var services = _services!;
        services.Get<OptionsStore>(store =>
            services.Get<IGameSettings>(settings =>
                services.Get<OptionsApplier>(applier => applier.ApplyMouse(store.Load(), settings))));
    }
}
