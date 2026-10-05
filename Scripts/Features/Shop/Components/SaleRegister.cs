using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     The checkout counter. Interacting sells one card: the card the player is holding, otherwise the
///     first sellable card on the nearest shelf. The price goes straight into <see cref="IMoneyService" />.
///     A label above the counter shows the balance and the result of the last sale.
/// </summary>
public partial class SaleRegister : StaticBody3D, IInteractable
{
    private const float DefaultInteractionRange = 4.5f;
    private const uint InteractableLayerBit = 4; // Layer 3, in the InteractionSystem mask

    private IMoneyService? _money;
    private string _message = string.Empty;

    /// <summary>The player's hand. Its top card is sold first.</summary>
    [Export]
    public CardHolder? PlayerHolder { get; set; }

    [Export]
    public Label3D? StatusLabel { get; set; }

    [Export]
    public Node3D? HighlightMesh { get; set; }

    [Export]
    public float InteractionRange { get; set; } = DefaultInteractionRange;

    /// <summary>Resolved from the service locator at startup; assignable directly in tests.</summary>
    public IMoneyService? Money
    {
        get => _money;
        set
        {
            Unsubscribe();
            _money = value;
            Subscribe();
            ShowIdle();
        }
    }

    public bool CanInteract => true;

    public Node3D InteractionBody => this;

    public override void _Ready()
    {
        CollisionLayer |= InteractableLayerBit;
        ClearHighlight();
        ServiceLocator.Get<IMoneyService>(money => Money = money);
        ShowIdle();
    }

    public override void _EnterTree()
    {
        // A register that leaves and re-enters the tree must listen again and show the current balance.
        Unsubscribe();
        Subscribe();
        if (_money != null)
            SetLabel(_money.Balance, null);
    }

    public override void _ExitTree() => Unsubscribe();

    public void Interact() => Sell();

    public void Highlight()
    {
        if (HighlightMesh != null)
            HighlightMesh.Visible = true;
    }

    public void ClearHighlight()
    {
        if (HighlightMesh != null)
            HighlightMesh.Visible = false;
    }

    /// <summary>Sells one card and reports the outcome on the label.</summary>
    public SaleResult Sell()
    {
        if (_money == null)
        {
            SetMessage("The register is unavailable right now.");
            return new SaleResult(SaleStatus.NothingToSell);
        }

        var result = SellHeldOrStocked(_money);
        SetMessage(result.Status switch
        {
            SaleStatus.Sold => $"Sold for {result.Price}.",
            SaleStatus.NotSellable => "That cannot be sold.",
            SaleStatus.AlreadySold => "That card was already sold.",
            _ => "Nothing to sell."
        });
        return result;
    }

    private SaleResult SellHeldOrStocked(IMoneyService money)
    {
        if (PlayerHolder is { HasCards: true } holder)
            return CardSale.TrySell(holder.HeldCards[^1], money, c =>
            {
                holder.RemoveCard(c);
                return true;
            });

        var result = new SaleResult(SaleStatus.NothingToSell);
        foreach (var shelf in ShelvesByDistance())
        {
            foreach (var card in shelf.StockedCards.ToArray())
            {
                // Something that cannot be sold stays on the shelf, so try the next one.
                var attempt = CardSale.TrySell(card, money, shelf.Release);
                if (attempt.Succeeded)
                    return attempt;
                result = attempt;
            }
        }

        return result;
    }

    private CardShelf[] ShelvesByDistance()
    {
        return GetTree().GetNodesInGroup(CardShelf.GroupName)
            .OfType<CardShelf>()
            .OrderBy(shelf => shelf.GlobalPosition.DistanceSquaredTo(GlobalPosition))
            .ToArray();
    }

    private void Subscribe()
    {
        if (_money != null)
            _money.BalanceChanged += OnBalanceChanged;
    }

    private void Unsubscribe()
    {
        if (_money != null)
            _money.BalanceChanged -= OnBalanceChanged;
    }

    private void OnBalanceChanged(int balance) => SetLabel(balance, null);

    private void ShowIdle() => SetLabel(_money?.Balance, "Interact to sell a card.");

    private void SetMessage(string message) => SetLabel(_money?.Balance, message);

    private void SetLabel(int? balance, string? message)
    {
        if (message != null)
            _message = message;
        if (StatusLabel == null)
            return;

        var balanceText = balance.HasValue ? $"Balance: {balance.Value}" : "Balance: -";
        StatusLabel.Text = string.IsNullOrEmpty(_message) ? balanceText : $"{balanceText}\n{_message}";
    }
}
