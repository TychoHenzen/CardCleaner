using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.GridMapGeneration;

internal readonly record struct VariantSelection(
    Dictionary<string, int> PerGenerationVariants,
    Dictionary<string, string> PerGenerationGroupVariants);
