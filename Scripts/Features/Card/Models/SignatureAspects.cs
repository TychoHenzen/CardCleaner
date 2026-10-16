using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Utilities;

namespace CardCleaner.Scripts.Features.Card.Models;

internal static class SignatureAspects
{
    public static Aspect GetDominantAspect(CardSignature signature, Element element) =>
        signature[element] >= 0 ? element.Positive() : element.Negative();
}
