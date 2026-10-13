using System.Text.Json.Serialization;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>The five numbers the dependency gate ratchets (#240 requirement 9), as stored in the baseline file.</summary>
internal sealed record GatedNumbers(
    [property: JsonPropertyName("up_edges")] int UpEdges,
    [property: JsonPropertyName("biggest_loop")] int BiggestLoop,
    [property: JsonPropertyName("seams_leaky")] int SeamsLeaky,
    [property: JsonPropertyName("seams_missing")] int SeamsMissing,
    [property: JsonPropertyName("refs_from_non_ui_to_ui")] int RefsFromNonUiToUi);
