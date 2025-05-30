using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Controllers;

[Tool]
public partial class BlacklightController : Node, IBlacklightController
{
    private ShaderMaterial _activeMaterial;

    private RigidBody3D _cardRoot;
    [Export] public float BlacklightRange { get; set; } = 5.0f;

    public void Setup(RigidBody3D cardRoot)
    {
        _cardRoot = cardRoot;
    }

    public float CalculateExposure(Vector3 cardPosition)
    {
        var player = GetTree().GetFirstNodeInGroup("player");
        var spotlight = player?.GetNodeOrNull<SpotLight3D>("Head/Camera3D/SpotLight3D");

        if (spotlight is not { Visible: true }) return 0.0f;

        var lightPos = spotlight.GlobalPosition;
        var lightForward = -spotlight.GlobalTransform.Basis.Z;

        var distance = lightPos.DistanceTo(cardPosition);
        if (distance > BlacklightRange) return 0.0f;

        var distanceFactor = 1.0f - distance / BlacklightRange;

        var toCard = (cardPosition - lightPos).Normalized();
        var angle = lightForward.Dot(toCard);
        var spotAngleRad = Mathf.DegToRad(spotlight.SpotAngle);

        if (angle < Mathf.Cos(spotAngleRad)) return 0.0f;

        var angleFactor = (angle - Mathf.Cos(spotAngleRad)) / (1.0f - Mathf.Cos(spotAngleRad));
        return Mathf.Clamp(distanceFactor * angleFactor * spotlight.LightEnergy, 0.0f, 1.0f);
    }

    public void UpdateBlacklightEffect(ShaderMaterial material)
    {
        if (_cardRoot == null || material == null) return;

        _activeMaterial = material;
    }

    public void IntegrateForces(PhysicsDirectBodyState3D state)
    {
        //no-op
    }

    public void PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || _cardRoot == null || _activeMaterial == null) return;

        var exposure = CalculateExposure(_cardRoot.GlobalPosition);
        _activeMaterial.SetShaderParameter("blacklight_exposure", exposure);
    }
}