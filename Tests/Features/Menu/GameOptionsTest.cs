using CardCleaner.Scripts.Features.Menu.Models;
using GdUnit4;

namespace CardCleaner.Tests.Features.Menu;

[TestSuite]
[RequireGodotRuntime]
public class GameOptionsTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void Clamped_limits_every_field()
    {
        var options = new GameOptions
        {
            MouseSensitivity = 9f, InvertY = false, MasterVolume = -1f, MusicVolume = 2f, SfxVolume = 0.5f,
            Fullscreen = true, ResolutionIndex = 99, Vsync = false,
        };

        var clamped = options.Clamped();

        Assertions.AssertFloat(clamped.MouseSensitivity).IsEqual(GameOptions.MaxMouseSensitivity);
        Assertions.AssertFloat(clamped.MasterVolume).IsEqual(0f);
        Assertions.AssertFloat(clamped.MusicVolume).IsEqual(1f);
        Assertions.AssertFloat(clamped.SfxVolume).IsEqual(0.5f);
        Assertions.AssertThat(clamped.ResolutionIndex).IsEqual(GameOptions.Resolutions.Length - 1);
        Assertions.AssertThat(clamped.Fullscreen).IsTrue();
        Assertions.AssertThat(clamped.Vsync).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Clamped_replaces_nan_with_the_default()
    {
        var options = new GameOptions
        {
            MouseSensitivity = float.NaN, InvertY = true, MasterVolume = float.NaN, MusicVolume = float.NaN,
            SfxVolume = float.NaN, Fullscreen = false, ResolutionIndex = -5, Vsync = true,
        };

        var clamped = options.Clamped();

        Assertions.AssertFloat(clamped.MouseSensitivity).IsEqual(GameOptions.Defaults.MouseSensitivity);
        Assertions.AssertFloat(clamped.MasterVolume).IsEqual(GameOptions.Defaults.MasterVolume);
        Assertions.AssertFloat(clamped.MusicVolume).IsEqual(GameOptions.Defaults.MusicVolume);
        Assertions.AssertFloat(clamped.SfxVolume).IsEqual(GameOptions.Defaults.SfxVolume);
        Assertions.AssertThat(clamped.ResolutionIndex).IsEqual(0);
        Assertions.AssertThat(clamped.InvertY).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Defaults_are_already_clamped()
    {
        Assertions.AssertThat(GameOptions.Defaults.Clamped()).IsEqual(GameOptions.Defaults);
    }
}
