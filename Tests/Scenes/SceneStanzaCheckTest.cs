namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The stanza check behind <see cref="SceneEditorRoundTripSceneTest" />: a node stanza or an editable line that the
/// file's canonical save has and an editor-style save lacks is reported, and nothing else.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SceneStanzaCheckTest
{
    private const string Fixtures = "res://Tests/Scenes/Fixtures";

    private const string Canonical = """
        [gd_scene load_steps=2 format=3 uid="uid://a"]

        [ext_resource type="PackedScene" uid="uid://b" path="res://Leaf.tscn" id="1_abc"]

        [node name="Root" type="Node3D" unique_id=11]

        [node name="Child" parent="." unique_id=12 instance=ExtResource("1_abc")]

        [node name="Leaf" parent="Child"]
        visible = false

        [node name="Sign" type="Label3D" parent="."]
        text = "name=\"Fake\""

        [editable path="Child"]
        """;

    [TestCase]
    [TestCategory("Unit")]
    public static void AnIdenticalSaveMissesNothing()
    {
        AssertThat(SceneStanzaCheck.Missing(Canonical, Canonical)).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void IdsOrderAndPropertiesDoNotCount()
    {
        const string saved = """
            [gd_scene format=3]

            [ext_resource type="PackedScene" path="res://Leaf.tscn" id="7_xyz"]

            [node name="Root" type="Node3D"]

            [node name="Sign" type="Label3D" parent="." parent_id_path=PackedInt32Array(11)]

            [editable path="Child"]

            [node name="Leaf" parent="Child"]

            [node name="Child" parent="." instance=ExtResource("7_xyz")]
            """;

        AssertThat(SceneStanzaCheck.Missing(Canonical, saved)).IsEmpty();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AMissingNodeAndEditableLineAreNamed()
    {
        const string saved = """
            [gd_scene format=3]

            [node name="Root" type="Node3D"]

            [node name="Child" parent="." instance=ExtResource("1_abc")]

            [node name="Sign" type="Label3D" parent="."]
            """;

        AssertThat(SceneStanzaCheck.Missing(Canonical, saved))
            .ContainsExactly("missing editable Child", "missing node Child/Leaf");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ANodeWithoutAParentAfterTheRootIsReported()
    {
        const string saved = """
            [gd_scene format=3]

            [node name="Root" type="Node3D"]

            [node name="Child" parent_id_path=PackedInt32Array(12)]
            """;

        AssertThat(SceneStanzaCheck.Missing(saved, saved))
            .ContainsExactly("unparsable node stanza [node name=\"Child\" parent_id_path=PackedInt32Array(12)]");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TextWithoutNodesIsReported()
    {
        AssertThat(SceneStanzaCheck.Missing("[gd_scene format=3]", "[gd_scene format=3]"))
            .ContainsExactly("no node stanza found");
    }

    // An override on a child of an instance that is not editable is dropped by an editor save (the editor writes only
    // what the outer scene owns), so its stanza disappears. The fixtures override only a group, which the node-property
    // comparison in SceneEditorRoundTripSceneTest does not see (groups are not stored properties). The fixtures run
    // through that test's own LostValues, so they prove the path every scene under res://Scenes takes.
    [TestCase]
    [TestCategory("Unit")]
    public static void AnOverrideOnANonEditableInstanceIsReported()
    {
        AssertThat(SceneEditorRoundTripSceneTest.LostValues($"{Fixtures}/RoundTripOverride.tscn"))
            .ContainsExactly("missing node Child/Leaf");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void AnOverrideOnAnEditableInstanceIsKept()
    {
        var path = $"{Fixtures}/RoundTripEditableOverride.tscn";

        AssertThat(SceneEditorRoundTripSceneTest.LostValues(path)).IsEmpty();
        AssertThat(SceneStanzaCheck.CanonicalKeys(path)).Contains("node Child/Leaf", "editable Child");
    }
}
