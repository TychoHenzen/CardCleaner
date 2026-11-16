namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     A screen that takes the cursor and the player's controls while it is open, such as the shop PC or the
///     checkout payment screen. The day report closes every open one before it shows, so the report is never
///     hidden behind a screen or left fighting it for the cursor and the player's controls.
/// </summary>
public interface IModalScreen
{
    /// <summary>Scene-tree group that every modal screen joins when it enters the tree.</summary>
    const string GroupName = "modal_screens";

    /// <summary>Closes the screen if it is open and gives back the cursor and the player's controls.</summary>
    void Close();
}
