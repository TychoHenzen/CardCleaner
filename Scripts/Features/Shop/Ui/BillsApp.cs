using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     The Bills app on the backoffice PC. It lists the bill lines of the current day from
///     <see cref="IDailyBillBook" />, with their total. It refreshes when it is shown and when the shop opens,
///     restores or closes.
/// </summary>
public partial class BillsApp : Control, IDependent
{
    private const string EmptyMessage = "No bills today.";

    private IServiceResolver? _services;
    private IDailyBillBook? _bills;
    private IShopDayService? _days;
    private VBoxContainer _rows = null!;
    private Label _dayLabel = null!;
    private Label _emptyLabel = null!;
    private Label _totalLabel = null!;
    private bool _layoutBuilt;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        Visible = false;
        EnsureLayout();
        VisibilityChanged += OnVisibilityChanged;

        var services = _services;
        if (services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        services.Get<IDailyBillBook>(bills => _bills = bills);
        services.Get<IShopDayService>(days =>
        {
            _days = days;
            Subscribe();
        });
    }

    // _Ready runs once, but the app can leave and re-enter the tree, so its subscriptions are restored
    // on every entry and dropped on every exit.
    public override void _EnterTree() => Subscribe();

    public override void _ExitTree() => Unsubscribe();

    /// <summary>
    ///     Shows the bill lines and total of the day the shop reports now. An empty day shows the empty message.
    /// </summary>
    public void Refresh()
    {
        EnsureLayout();
        if (_bills == null || _days == null)
            return;

        var day = _days.Day;
        var lines = _bills.LinesFor(day);
        _dayLabel.Text = $"Day {day}";
        ClearRows();
        foreach (var line in lines)
            _rows.AddChild(BuildRow(line));

        var hasLines = lines.Count > 0;
        _emptyLabel.Text = hasLines ? "" : EmptyMessage;
        _emptyLabel.Visible = !hasLines;
        _totalLabel.Text = hasLines ? $"Total: {_bills.TotalFor(day)}" : "";
        _totalLabel.Visible = hasLines;
    }

    internal int RowCount
    {
        get
        {
            EnsureLayout();
            return _rows.GetChildCount();
        }
    }

    internal string TotalText
    {
        get
        {
            EnsureLayout();
            return _totalLabel.Text;
        }
    }

    internal string EmptyText
    {
        get
        {
            EnsureLayout();
            return _emptyLabel.Text;
        }
    }

    internal string DayText
    {
        get
        {
            EnsureLayout();
            return _dayLabel.Text;
        }
    }

    private void OnVisibilityChanged()
    {
        if (Visible)
            Refresh();
    }

    private void OnClosed(DayReport report) => Refresh();

    private void Subscribe()
    {
        if (!IsInsideTree() || _days == null)
            return;

        Unsubscribe();
        _days.Opened += Refresh;
        _days.Restored += Refresh;
        _days.Closed += OnClosed;
    }

    private void Unsubscribe()
    {
        if (_days == null)
            return;

        _days.Opened -= Refresh;
        _days.Restored -= Refresh;
        _days.Closed -= OnClosed;
    }

    private void ClearRows()
    {
        // Free() detaches the row and frees it at once, so no detached node waits for a frame as an orphan.
        // Refresh is never raised by a row's own signal, so freeing here is safe.
        foreach (var child in _rows.GetChildren())
            child.Free();
    }

    private static HBoxContainer BuildRow(BillLine line)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = line.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill });
        row.AddChild(new Label { Text = line.Amount.ToString() });
        return row;
    }

    // ASSUMPTION: the layout is built on first use, not only in _Ready, so the test hooks work on a node that
    // has not entered the tree yet (Low risk).
    private void EnsureLayout()
    {
        if (_layoutBuilt)
            return;
        _layoutBuilt = true;

        var column = new VBoxContainer();
        column.SetAnchorsPreset(LayoutPreset.FullRect);
        _dayLabel = new Label();
        _rows = new VBoxContainer();
        _emptyLabel = new Label();
        _totalLabel = new Label();
        column.AddChild(_dayLabel);
        column.AddChild(_rows);
        column.AddChild(_emptyLabel);
        column.AddChild(_totalLabel);
        AddChild(column);
    }
}
