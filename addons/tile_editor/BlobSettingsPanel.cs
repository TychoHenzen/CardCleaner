#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Panel for editing blob generation settings.
/// Controls noise-based terrain clustering parameters.
/// </summary>
[Tool]
public partial class BlobSettingsPanel : ScrollContainer
{
    private readonly TileEditorService _service;
    private CheckBox? _enabledCheckBox;
    private SpinBox? _noiseScaleSpinBox;
    private SpinBox? _clusterStrengthSpinBox;
    private SpinBox? _minBlobSizeSpinBox;
    private SpinBox? _maxBlobSizeSpinBox;
    private bool _isUpdating;

    public BlobSettingsPanel(TileEditorService service)
    {
        _service = service;
        _service.TilesLoaded += RefreshDisplay;
        _service.BlobConfigModified += RefreshDisplay;

        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        SetupUI();
    }

    private void SetupUI()
    {
        var vbox = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(vbox);

        var headerLabel = new Label
        {
            Text = "Blob Generation Settings",
            ThemeTypeVariation = "HeaderMedium"
        };
        vbox.AddChild(headerLabel);

        var descLabel = new Label
        {
            Text = "Controls how terrain tiles are clustered into coherent regions\nusing noise-based selection.",
            AutowrapMode = TextServer.AutowrapMode.Word
        };
        vbox.AddChild(descLabel);

        vbox.AddChild(new HSeparator());

        // Enabled checkbox
        var enabledRow = new HBoxContainer();
        vbox.AddChild(enabledRow);
        _enabledCheckBox = new CheckBox { Text = "Enable Blob Generation" };
        _enabledCheckBox.Toggled += OnEnabledToggled;
        enabledRow.AddChild(_enabledCheckBox);

        vbox.AddChild(new HSeparator());

        // Noise Scale
        var noiseScaleRow = new HBoxContainer();
        vbox.AddChild(noiseScaleRow);
        noiseScaleRow.AddChild(new Label { Text = "Noise Scale:", CustomMinimumSize = new Vector2(140, 0) });
        _noiseScaleSpinBox = new SpinBox
        {
            MinValue = 0.01,
            MaxValue = 1.0,
            Step = 0.01,
            Value = 0.15,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _noiseScaleSpinBox.ValueChanged += OnNoiseScaleChanged;
        noiseScaleRow.AddChild(_noiseScaleSpinBox);

        var noiseScaleDesc = new Label
        {
            Text = "Controls blob size (lower = larger blobs)",
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        vbox.AddChild(noiseScaleDesc);

        // Cluster Strength
        var clusterRow = new HBoxContainer();
        vbox.AddChild(clusterRow);
        clusterRow.AddChild(new Label { Text = "Cluster Strength:", CustomMinimumSize = new Vector2(140, 0) });
        _clusterStrengthSpinBox = new SpinBox
        {
            MinValue = 0.0,
            MaxValue = 1.0,
            Step = 0.05,
            Value = 0.7,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _clusterStrengthSpinBox.ValueChanged += OnClusterStrengthChanged;
        clusterRow.AddChild(_clusterStrengthSpinBox);

        var clusterDesc = new Label
        {
            Text = "How strongly tiles cluster (0 = random, 1 = strict)",
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        vbox.AddChild(clusterDesc);

        vbox.AddChild(new HSeparator());

        // Min Blob Size
        var minSizeRow = new HBoxContainer();
        vbox.AddChild(minSizeRow);
        minSizeRow.AddChild(new Label { Text = "Min Blob Size:", CustomMinimumSize = new Vector2(140, 0) });
        _minBlobSizeSpinBox = new SpinBox
        {
            MinValue = 1,
            MaxValue = 50,
            Step = 1,
            Value = 3,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _minBlobSizeSpinBox.ValueChanged += OnMinBlobSizeChanged;
        minSizeRow.AddChild(_minBlobSizeSpinBox);

        // Max Blob Size
        var maxSizeRow = new HBoxContainer();
        vbox.AddChild(maxSizeRow);
        maxSizeRow.AddChild(new Label { Text = "Max Blob Size:", CustomMinimumSize = new Vector2(140, 0) });
        _maxBlobSizeSpinBox = new SpinBox
        {
            MinValue = 1,
            MaxValue = 100,
            Step = 1,
            Value = 12,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _maxBlobSizeSpinBox.ValueChanged += OnMaxBlobSizeChanged;
        maxSizeRow.AddChild(_maxBlobSizeSpinBox);

        var sizeDesc = new Label
        {
            Text = "Target size range for terrain blobs (in tiles)",
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        vbox.AddChild(sizeDesc);
    }

    private void RefreshDisplay()
    {
        _isUpdating = true;
        try
        {
            var config = _service.BlobConfig;
            _enabledCheckBox!.ButtonPressed = config.Enabled;
            _noiseScaleSpinBox!.Value = config.NoiseScale;
            _clusterStrengthSpinBox!.Value = config.ClusterStrength;
            _minBlobSizeSpinBox!.Value = config.MinBlobSize;
            _maxBlobSizeSpinBox!.Value = config.MaxBlobSize;

            UpdateControlStates();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void UpdateControlStates()
    {
        var enabled = _enabledCheckBox!.ButtonPressed;
        _noiseScaleSpinBox!.Editable = enabled;
        _clusterStrengthSpinBox!.Editable = enabled;
        _minBlobSizeSpinBox!.Editable = enabled;
        _maxBlobSizeSpinBox!.Editable = enabled;
    }

    private void OnEnabledToggled(bool enabled)
    {
        if (_isUpdating) return;
        UpdateAndSave();
        UpdateControlStates();
    }

    private void OnNoiseScaleChanged(double value)
    {
        if (_isUpdating) return;
        UpdateAndSave();
    }

    private void OnClusterStrengthChanged(double value)
    {
        if (_isUpdating) return;
        UpdateAndSave();
    }

    private void OnMinBlobSizeChanged(double value)
    {
        if (_isUpdating) return;

        // Ensure min <= max
        if (value > _maxBlobSizeSpinBox!.Value)
        {
            _maxBlobSizeSpinBox.Value = value;
        }

        UpdateAndSave();
    }

    private void OnMaxBlobSizeChanged(double value)
    {
        if (_isUpdating) return;

        // Ensure max >= min
        if (value < _minBlobSizeSpinBox!.Value)
        {
            _minBlobSizeSpinBox.Value = value;
        }

        UpdateAndSave();
    }

    private void UpdateAndSave()
    {
        var config = new EditableBlobConfig
        {
            Enabled = _enabledCheckBox!.ButtonPressed,
            NoiseScale = (float)_noiseScaleSpinBox!.Value,
            ClusterStrength = (float)_clusterStrengthSpinBox!.Value,
            MinBlobSize = (int)_minBlobSizeSpinBox!.Value,
            MaxBlobSize = (int)_maxBlobSizeSpinBox!.Value
        };

        _service.UpdateBlobConfig(config);
    }
}
#endif
