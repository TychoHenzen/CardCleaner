using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Features.Menu.Models;
using CardCleaner.Scripts.Features.Menu.Services;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Menu;

/// <summary>
/// Applies options to the real audio buses and to a GameSettings node. Window mode is not asserted here: headless
/// there is no real window, so that check belongs to the manual look.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class OptionsApplierTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void Master_volume_half_sets_master_bus_to_about_minus_6_dB()
    {
        var master = ApplyAndReadMasterDb(GameOptions.Defaults with { MasterVolume = 0.5f });

        Assertions.AssertFloat(master).IsGreater(-6.1f);
        Assertions.AssertFloat(master).IsLess(-5.9f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Master_volume_zero_sets_master_bus_to_minus_80_dB_or_lower()
    {
        var master = ApplyAndReadMasterDb(GameOptions.Defaults with { MasterVolume = 0f });

        Assertions.AssertFloat(master).IsLessEqual(-80f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ApplyMouse_sets_sensitivity_and_invert_on_the_settings()
    {
        var settings = new GameSettings();
        try
        {
            var options = GameOptions.Defaults with { MouseSensitivity = 0.25f, InvertY = true };

            new OptionsApplier().ApplyMouse(options, settings);

            Assertions.AssertFloat(settings.MouseSensitivity).IsEqual(0.25f);
            Assertions.AssertThat(settings.InvertY).IsTrue();
        }
        finally
        {
            settings.Free();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Apply_does_not_touch_the_mouse_settings()
    {
        var settings = new GameSettings { MouseSensitivity = 0.3f };
        var window = new Window();
        try
        {
            new OptionsApplier().Apply(GameOptions.Defaults with { MouseSensitivity = 0.05f }, window);

            Assertions.AssertFloat(settings.MouseSensitivity).IsEqual(0.3f);
        }
        finally
        {
            new OptionsApplier().Apply(GameOptions.Defaults, window);
            window.Free();
            settings.Free();
        }
    }

    private static float ApplyAndReadMasterDb(GameOptions options)
    {
        var window = new Window();
        try
        {
            new OptionsApplier().Apply(options, window);
            return AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Master"));
        }
        finally
        {
            // Restore the default volume so later suites hear the master bus at unity gain.
            new OptionsApplier().Apply(GameOptions.Defaults, window);
            window.Free();
        }
    }
}
