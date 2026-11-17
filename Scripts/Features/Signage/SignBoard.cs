using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Signage;

/// <summary>
///     A sign as a real shop has one: a board with its text printed flat on the face, lit by the room like everything
///     around it. It is fixed flat to a wall, hung from the ceiling on two rods, or stood on a post. A two-sided board
///     prints the text on its back as well, so it reads from both sides, and a screen board is a dark panel whose text
///     glows instead of taking the room's light. Nothing turns to face the camera.
///     The board, text and mount are built as internal children, which no scene stores, so a scene file holds only the
///     exports. It is a tool script so the editor shows the sign as the game does; it changes no node a scene saves.
/// </summary>
[Tool]
public partial class SignBoard : Node3D
{
    /// <summary>How the board is held up.</summary>
    public enum MountKind
    {
        /// <summary>The back of the board lies on a wall; nothing else is drawn.</summary>
        Wall,

        /// <summary>Two rods rise <see cref="MountLength" /> from the top edge to the ceiling.</summary>
        Hanging,

        /// <summary>One post drops <see cref="MountLength" /> from the bottom edge to what it stands on.</summary>
        Post
    }

    private const float TextLift = 0.002f;
    private const float TextFill = 0.92f;
    private const float RodRadius = 0.008f;
    private const float PostRadius = 0.02f;
    private const float RodInset = 0.15f;
    private static readonly Color MountColor = new(0.25f, 0.25f, 0.27f);
    private static readonly Color ScreenColor = new(0.02f, 0.03f, 0.03f);

    private readonly List<Node> _built = new();
    private string _text = string.Empty;
    private Vector2 _size = new(1.2f, 0.4f);
    private float _thickness = 0.03f;
    private Color _boardColor = new(0.13f, 0.2f, 0.3f);
    private Color _textColor = new(0.95f, 0.93f, 0.86f);
    private int _fontSize = 64;
    private float _pixelSize = 0.004f;
    private bool _twoSided;
    private bool _screen;
    private MountKind _mount = MountKind.Wall;
    private float _mountLength;

    /// <summary>
    ///     What the board says. Changing it repaints both faces at once; setting the text it already shows does
    ///     nothing, so a display can be given its text every frame.
    /// </summary>
    [Export(PropertyHint.MultilineText)]
    public string Text
    {
        get => _text;
        set
        {
            var text = value ?? string.Empty;
            if (text == _text)
                return;
            _text = text;
            foreach (var face in Faces)
                face.Text = _text;
        }
    }

    /// <summary>Width and height of the board in metres.</summary>
    [Export]
    public Vector2 Size
    {
        get => _size;
        set => Change(ref _size, value);
    }

    [Export]
    public float Thickness
    {
        get => _thickness;
        set => Change(ref _thickness, value);
    }

    [Export]
    public Color BoardColor
    {
        get => _boardColor;
        set => Change(ref _boardColor, value);
    }

    /// <summary>The colour of the print. Changing it repaints both faces at once, as <see cref="Text" /> does.</summary>
    [Export]
    public Color TextColor
    {
        get => _textColor;
        set
        {
            _textColor = value;
            foreach (var face in Faces)
                face.Modulate = _textColor;
        }
    }

    [Export]
    public int FontSize
    {
        get => _fontSize;
        set => Change(ref _fontSize, value);
    }

    /// <summary>Metres per font pixel: with <see cref="FontSize" /> it sets how tall a letter stands.</summary>
    [Export]
    public float PixelSize
    {
        get => _pixelSize;
        set => Change(ref _pixelSize, value);
    }

    /// <summary>Prints the text on the back too, for a board seen from both sides.</summary>
    [Export]
    public bool TwoSided
    {
        get => _twoSided;
        set => Change(ref _twoSided, value);
    }

    /// <summary>A dark panel with glowing text, such as a register display, instead of a painted board.</summary>
    [Export]
    public bool Screen
    {
        get => _screen;
        set => Change(ref _screen, value);
    }

    [Export]
    public MountKind Mount
    {
        get => _mount;
        set => Change(ref _mount, value);
    }

    /// <summary>Length of the rods or the post in metres; nothing is drawn while it is zero.</summary>
    [Export]
    public float MountLength
    {
        get => _mountLength;
        set => Change(ref _mountLength, value);
    }

    /// <summary>The text on the front face, or null before the sign is built.</summary>
    internal Label3D? FrontFace { get; private set; }

    /// <summary>The text on the back face, or null for a one-sided sign.</summary>
    internal Label3D? BackFace { get; private set; }

    /// <summary>The built rods or post, empty for a wall sign.</summary>
    internal IReadOnlyList<MeshInstance3D> MountParts { get; private set; } = [];

    /// <summary>
    ///     True when the text, set unwrapped in the face's font, fits inside the printable part of the board: every
    ///     line within the margins and all lines within its height.
    /// </summary>
    public bool TextFits
    {
        get
        {
            // The font a Label3D without its own font prints in: the project theme's, else the default theme's.
            var font = FrontFace?.Font ?? ThemeDB.GetProjectTheme()?.DefaultFont ??
                ThemeDB.GetDefaultTheme()?.DefaultFont ?? ThemeDB.FallbackFont;
            var printed = font.GetMultilineStringSize(_text, HorizontalAlignment.Center, -1f, _fontSize) * _pixelSize;
            return printed.X <= _size.X * TextFill && printed.Y <= _size.Y;
        }
    }

    private IEnumerable<Label3D> Faces
    {
        get
        {
            if (FrontFace != null)
                yield return FrontFace;
            if (BackFace != null)
                yield return BackFace;
        }
    }

    public override void _Ready() => Build();

    private void Change<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        if (IsNodeReady())
            Build();
    }

    private void Build()
    {
        foreach (var node in _built)
            node.QueueFree();
        _built.Clear();

        var board = new MeshInstance3D
        {
            Name = "Board",
            Mesh = new BoxMesh
            {
                Size = new Vector3(_size.X, _size.Y, _thickness),
                Material = new StandardMaterial3D { AlbedoColor = _screen ? ScreenColor : _boardColor }
            }
        };
        Add(board);

        FrontFace = Add(Face("Front", _thickness / 2f + TextLift, 0f));
        BackFace = _twoSided ? Add(Face("Back", -(_thickness / 2f + TextLift), Mathf.Pi)) : null;
        MountParts = BuildMount();
    }

    private Label3D Face(string name, float depth, float turn) => new()
    {
        Name = name,
        Text = _text,
        FontSize = _fontSize,
        PixelSize = _pixelSize,
        Modulate = _textColor,
        OutlineSize = 0,
        Width = _size.X * TextFill / _pixelSize,
        // Breaks only between words: a word too long for the board runs over its edge, which TextFits reports,
        // rather than being cut in two.
        AutowrapMode = TextServer.AutowrapMode.Word,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        // Printed text takes the room's light like its board; a screen's text glows.
        Shaded = !_screen,
        DoubleSided = false,
        AlphaCut = Label3D.AlphaCutMode.OpaquePrepass,
        Position = new Vector3(0f, 0f, depth),
        Rotation = new Vector3(0f, turn, 0f)
    };

    private IReadOnlyList<MeshInstance3D> BuildMount()
    {
        if (_mount == MountKind.Wall || _mountLength <= 0f)
            return [];

        var material = new StandardMaterial3D { AlbedoColor = MountColor, Metallic = 0.6f, Roughness = 0.4f };
        if (_mount == MountKind.Post)
        {
            var post = Rod("Post", PostRadius, material);
            post.Position = new Vector3(0f, -(_size.Y + _mountLength) / 2f, 0f);
            return [Add(post)];
        }

        var inset = Mathf.Min(RodInset, _size.X / 4f);
        var parts = new List<MeshInstance3D>();
        foreach (var side in new[] { -1f, 1f })
        {
            var rod = Rod(side < 0 ? "RodLeft" : "RodRight", RodRadius, material);
            rod.Position = new Vector3(side * (_size.X / 2f - inset), (_size.Y + _mountLength) / 2f, 0f);
            parts.Add(Add(rod));
        }

        return parts;
    }

    private MeshInstance3D Rod(string name, float radius, Material material) => new()
    {
        Name = name,
        Mesh = new CylinderMesh
        {
            TopRadius = radius, BottomRadius = radius, Height = _mountLength, RadialSegments = 8,
            Material = material
        }
    };

    // Internal children are left out of every scene save and of GetChildren, so the sign stores only its exports.
    private T Add<T>(T node) where T : Node
    {
        AddChild(node, false, InternalMode.Front);
        _built.Add(node);
        return node;
    }
}
