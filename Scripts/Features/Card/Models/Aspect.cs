using System.Collections.Generic;

namespace CardCleaner.Scripts
{
    public enum Aspect
    {
        Ignis, //fire
        Hydris, // water
        Tellus, // earth
        Aeolis, // air
        Empyrus, // chaos
        Vitrio, // order
        Luminus, // light
        Noctis, // dark
        Spatius, // Space
        Tempus, // Time
        Gravitas, // Heavy
        Levitas, // light
        Auxillus, // Helpful
        Malus, // Harmful
        Iuxta, // Nearby
        Disis // Distant
    }

    public enum Element
    {
        Solidum, // solidity (air-rock)
        Febris, // temperature (water-fire)
        Ordinem, // orderedness (entropy-order)
        Lumines, // luminance (dark-light)
        Varias, // Manifold (time-space)
        Inertiae, // Density (heavy-light)
        Subsidium, // Helpfulness (harmful-helpful)
        Spatium // Distance (nearby-distant)
    }

    public static class EleAspects
    {
        public static Aspect Positive(this Element e)
        {
            Dictionary<Element, Aspect> conversion = new Dictionary<Element, Aspect>()
            {
                { Element.Solidum, Aspect.Tellus },
                { Element.Febris, Aspect.Ignis },
                { Element.Ordinem, Aspect.Vitrio },
                { Element.Lumines, Aspect.Luminus },
                { Element.Varias, Aspect.Spatius },
                { Element.Inertiae, Aspect.Gravitas },
                { Element.Subsidium, Aspect.Auxillus },
                { Element.Spatium, Aspect.Disis }
            };
            return conversion[e];
        }

        public static Aspect Negative(this Element e)
        {
            Dictionary<Element, Aspect> conversion = new Dictionary<Element, Aspect>()
            {
                { Element.Solidum, Aspect.Aeolis },
                { Element.Febris, Aspect.Hydris },
                { Element.Ordinem, Aspect.Empyrus },
                { Element.Lumines, Aspect.Noctis },
                { Element.Varias, Aspect.Tempus },
                { Element.Inertiae, Aspect.Levitas },
                { Element.Subsidium, Aspect.Malus },
                { Element.Spatium, Aspect.Iuxta }
            };
            return conversion[e];
        }

        public static Element IsElement(this Aspect a)
        {
            Dictionary<Aspect, Element> conversion = new Dictionary<Aspect, Element>()
            {
                { Aspect.Aeolis, Element.Solidum },
                { Aspect.Hydris, Element.Febris },
                { Aspect.Empyrus, Element.Ordinem },
                { Aspect.Noctis, Element.Lumines },
                { Aspect.Tempus, Element.Varias },
                { Aspect.Levitas, Element.Inertiae },
                { Aspect.Malus, Element.Subsidium },
                { Aspect.Iuxta, Element.Spatium },

                { Aspect.Tellus, Element.Solidum },
                { Aspect.Ignis, Element.Febris },
                { Aspect.Vitrio, Element.Ordinem },
                { Aspect.Luminus, Element.Lumines },
                { Aspect.Spatius, Element.Varias },
                { Aspect.Gravitas, Element.Inertiae },
                { Aspect.Auxillus, Element.Subsidium },
                { Aspect.Disis, Element.Spatium }
            };
            return conversion[a];
        }
    }
}
