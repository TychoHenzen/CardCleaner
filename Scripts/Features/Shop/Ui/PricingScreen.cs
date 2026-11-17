using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Pause.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     The pricing screen a shelf price tag opens. It shows what is being priced and what the price covers, the market
///     price, the wholesale cost and profit per sale when the stock was bought in, and the share of customers who buy
///     at the price in the entry, which updates as the entry changes. Set price applies the entry; Cancel or Escape
///     closes without a change. While open it holds the cursor and the player's controls.
/// </summary>
[Service(typeof(IPricingScreen))]
public partial class PricingScreen : CanvasLayer, IDependent, IModalScreen, IPricingScreen
{
    private const int PanelWidth = 440;
    private const int Margin = 16;
    private const int TitleFontSize = 26;
    private const string CloseAction = "ui_cancel";

    private IServiceResolver? _services;
    private ICursorOwner? _cursor;
    private PlayerController? _player;
    private IPricedStock? _stock;
    private Label _title = null!;
    private Label _scope = null!;
    private Label _figures = null!;
    private SpinBox _entry = null!;
    private Button _lower = null!;
    private Button _raise = null!;
    private Button _market = null!;
    private Button _apply = null!;
    private Button _cancel = null!;

    public bool IsOpen => Visible;

    public Label Title => _title;

    public Label ScopeText => _scope;

    public Label Figures => _figures;

    /// <summary>The price being weighed. Typing, the arrows and the mouse wheel change it.</summary>
    public SpinBox Entry => _entry;

    public Button LowerButton => _lower;

    public Button RaiseButton => _raise;

    public Button MarketButton => _market;

    public Button ApplyButton => _apply;

    public Button CancelButton => _cancel;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        Visible = false;
        BuildLayout();
        _services?.Get<ICursorOwner>(cursor => _cursor = cursor);
    }

    public override void _EnterTree() => AddToGroup(IModalScreen.GroupName);

    // Freed while open: give the cursor and the player's control back.
    public override void _ExitTree() => Close();

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !@event.IsActionPressed(CloseAction))
            return;

        GetViewport().SetInputAsHandled();
        Close();
    }

    public void Open(IPricedStock stock)
    {
        _stock = stock;
        _title.Text = stock.ItemName;
        _scope.Text = stock.Scope;
        var price = ShopPrice.Clamp(stock.Price);
        _entry.Value = price;
        // A reopen at the same price sets no new value, so neither the text left from a cancelled edit nor the
        // figures of the previous stock would be replaced.
        _entry.GetLineEdit().Text = $"{price}";
        ShowFigures();

        if (Visible)
            return;

        Visible = true;
        _cursor?.Claim(this);
        _player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
        if (_player != null)
            _player.ControlEnabled = false;
    }

    // Idempotent: Set price, Cancel, Escape, leaving the tree and the day report may each call it.
    public void Close()
    {
        if (!Visible)
            return;

        Visible = false;
        _stock = null;
        _cursor?.Release(this);
        if (_player != null)
        {
            _player.ControlEnabled = true;
            _player = null;
        }
    }

    private int EntryPrice => ShopPrice.Clamp((long)_entry.Value);

    // The entry's value is current: typing updates it as the text changes (UpdateOnTextChanged), and its text can lag
    // behind a value set by the buttons, so the text is never read here.
    private void Apply()
    {
        _stock?.SetPrice(EntryPrice);
        Close();
    }

    private void ShowFigures()
    {
        if (_stock == null)
            return;

        _figures.Text = string.Join("\n", PricingSheet.Lines(_stock.MarketValue, _stock.UnitCost, EntryPrice));
    }

    private void BuildLayout()
    {
        var column = ScreenPanel.AddTo(this, PanelWidth, Margin);
        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", TitleFontSize);
        column.AddChild(_title);
        _scope = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        column.AddChild(_scope);
        _figures = new Label();
        column.AddChild(_figures);

        _entry = new SpinBox
        {
            MinValue = ShopPrice.Min,
            MaxValue = ShopPrice.Max,
            Step = 1,
            Rounded = true,
            UpdateOnTextChanged = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _entry.ValueChanged += _ => ShowFigures();
        _entry.GetLineEdit().TextSubmitted += _ => Apply();
        _lower = Plain("-", () => _entry.Value -= 1);
        _raise = Plain("+", () => _entry.Value += 1);
        column.AddChild(Row(new Label { Text = "Your price" }, _lower, _entry, _raise));

        _market = Plain("Market price", () => _entry.Value = _stock?.MarketValue ?? _entry.Value);
        _apply = Plain("Set price", Apply);
        _cancel = Plain("Cancel", Close);
        column.AddChild(Row(_market, _apply, _cancel));
    }

    private static HBoxContainer Row(params Control[] controls)
    {
        var row = new HBoxContainer();
        foreach (var control in controls)
            row.AddChild(control);
        return row;
    }

    private static Button Plain(string text, System.Action onPressed)
    {
        var button = new Button { Text = text };
        button.Pressed += onPressed;
        return button;
    }
}
