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

    [TestCase]
    [TestCategory("Unit")]
    public void DisplayFor_fullscreen_has_exclusive_mode_and_no_size()
    {
        var plan = OptionsApplier.DisplayFor(GameOptions.Defaults with { Fullscreen = true, ResolutionIndex = 2 });

        Assertions.AssertThat(plan).IsEqual(
            new DisplayPlan(DisplayServer.WindowMode.ExclusiveFullscreen, null, DisplayServer.VSyncMode.Enabled));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DisplayFor_windowed_uses_the_resolution_and_vsync()
    {
        var plan = OptionsApplier.DisplayFor(GameOptions.Defaults with { ResolutionIndex = 2, Vsync = true });

        Assertions.AssertThat(plan).IsEqual(
            new DisplayPlan(
                DisplayServer.WindowMode.Windowed, new Vector2I(1920, 1080), DisplayServer.VSyncMode.Enabled));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DisplayFor_vsync_off_is_Disabled()
    {
        var plan = OptionsApplier.DisplayFor(GameOptions.Defaults with { Vsync = false });

        Assertions.AssertThat(plan.VSync == DisplayServer.VSyncMode.Disabled).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DisplayFor_clamps_an_out_of_range_index()
    {
        var plan = OptionsApplier.DisplayFor(GameOptions.Defaults with { ResolutionIndex = 99 });

        var last = GameOptions.Resolutions[^1];
        Assertions.AssertThat(plan.Size).IsEqual(new Vector2I(last.Width, last.Height));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Apply_sends_the_display_plan_to_the_sink()
    {
        DisplayPlan? seen = null;
        Window? seenWindow = null;
        var window = new Window();
        var options = GameOptions.Defaults with { Fullscreen = true, Vsync = false, MasterVolume = 0.5f };
        try
        {
            new OptionsApplier((plan, win) =>
            {
                seen = plan;
                seenWindow = win;
            }).Apply(options, window);

            Assertions.AssertThat(seen).IsEqual(OptionsApplier.DisplayFor(options));
            Assertions.AssertThat(ReferenceEquals(seenWindow, window)).IsTrue();

            var master = AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Master"));
            Assertions.AssertFloat(master).IsGreater(-6.1f);
            Assertions.AssertFloat(master).IsLess(-5.9f);
        }
        finally
        {
            // Restore the buses through a sink that does not touch the window.
            new OptionsApplier(static (_, _) => { }).Apply(GameOptions.Defaults, window);
            window.Free();
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
