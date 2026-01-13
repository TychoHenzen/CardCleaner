using System;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Components;

/// <summary>
/// Manages combat UI elements during combat encounters.
/// </summary>
public class WorldMapCombatUI
{
    private readonly Control? _combatUI;
    private readonly ProgressBar? _playerHealthBar;
    private readonly ProgressBar? _enemyHealthBar;
    private readonly Label? _playerHealthLabel;
    private readonly Label? _enemyHealthLabel;
    private readonly Label? _actionLabel;

    public WorldMapCombatUI(Control? combatUI)
    {
        _combatUI = combatUI;
        if (_combatUI == null) return;

        _playerHealthBar = _combatUI.GetNode<ProgressBar>("PlayerHealthBar");
        _enemyHealthBar = _combatUI.GetNode<ProgressBar>("EnemyHealthBar");
        _playerHealthLabel = _combatUI.GetNode<Label>("PlayerHealthLabel");
        _enemyHealthLabel = _combatUI.GetNode<Label>("EnemyHealthLabel");
        _actionLabel = _combatUI.GetNode<Label>("ActionLabel");
    }

    /// <summary>
    /// Show the combat UI.
    /// </summary>
    public void Show()
    {
        if (_combatUI != null) _combatUI.Visible = true;
    }

    /// <summary>
    /// Hide the combat UI.
    /// </summary>
    public void Hide()
    {
        if (_combatUI != null) _combatUI.Visible = false;
    }

    /// <summary>
    /// Update the combat UI with current health values.
    /// </summary>
    public void UpdateHealth(int playerHealth, int playerMaxHealth, int enemyHealth, int enemyMaxHealth)
    {
        if (_playerHealthBar != null)
            _playerHealthBar.Value = (float)playerHealth / playerMaxHealth * 100f;

        if (_enemyHealthBar != null)
            _enemyHealthBar.Value = (float)enemyHealth / enemyMaxHealth * 100f;

        if (_playerHealthLabel != null)
            _playerHealthLabel.Text = $"Player: {Math.Max(0, playerHealth)}/{playerMaxHealth}";

        if (_enemyHealthLabel != null)
            _enemyHealthLabel.Text = $"Enemy: {Math.Max(0, enemyHealth)}/{enemyMaxHealth}";
    }

    /// <summary>
    /// Update the action text display.
    /// </summary>
    public void UpdateAction(string action)
    {
        if (_actionLabel != null) _actionLabel.Text = action;
    }
}
