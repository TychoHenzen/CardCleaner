# Card effects: where the port differs from Axiom2d

The card art effects are ported from Axiom2d. The code comments point here
for the places where the port does not match it.

## Region segmentation

`CardEffectRegionSegmenter` splits card art into flat colour regions by flood
fill, using Axiom2d's colour and alpha thresholds. The result is an
approximation:

- Not ported: the Scale2x upscale. Card art is 256 px and the working size is
  128 px, so the port only scales the art down, with nearest-neighbour
  sampling.
- Not ported: contour tracing. The regions are pixel sets, so they approximate
  the areas Axiom2d's shapes covered. They are not vector contours.

## Intensity

Axiom2d applies the 0.3 and 0.7 thresholds to one element's magnitude and draws
a card's tier from a seed. A card here needs one number, so
`CardEffectMapping.IntensityOf` uses the mean magnitude of the eight elements.
The strongest element was rejected. For a random signature, the strongest of
the eight elements is at least 0.7 about 94% of the time, which would make
nearly every card Intense, while the mean sits near 0.5.
