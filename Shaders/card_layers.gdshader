/* card_layers.gdshader */
shader_type spatial;
render_mode depth_prepass_alpha;

// Total number of layers = 27
const int LAYER_COUNT = 27;

// Per-layer uniforms (unchanged)
uniform sampler2D textures[LAYER_COUNT];
uniform vec4      regions[LAYER_COUNT];
uniform bool      frontFlags[LAYER_COUNT];
uniform bool      backFlags[LAYER_COUNT];

// Enhanced gem emission system
uniform vec3  gem_emission_colors[8];
uniform float gem_emission_strengths[8];

// Enhanced blacklight controls
uniform float blacklight_exposure : hint_range(0.0, 1.0) = 0.0;
uniform float emission_intensity : hint_range(0.0, 5.0) = 1.0;
uniform float saturation_boost : hint_range(1.0, 3.0) = 1.0;
uniform float ambient_emission : hint_range(0.0, 0.3) = 0.05;
uniform float strength_curve : hint_range(1.0, 4.0) = 1.3;

void fragment() {
    vec2 uv = UV;
    bool isFront = uv.x < 0.5;
    // Remap to [0,1] across the card face
    float half = 0.5;
    vec2 locUV = vec2(
        isFront ? uv.x/half : (uv.x - half)/half,
        uv.y
    );
    vec4 outCol = vec4(0.0);
    vec3 emission = vec3(0.0);

    // Blend each layer in order
    for (int i = 0; i < LAYER_COUNT; i++) {
        if ((isFront && frontFlags[i]) || (!isFront && backFlags[i])) {
            vec4 r = regions[i];
            if (locUV.x >= r.x && locUV.x <= r.x + r.z &&
                locUV.y >= r.y && locUV.y <= r.y + r.w) {
                vec2 sampleUV = (locUV - r.xy) / r.zw;
                vec4 sample = texture(textures[i], sampleUV);

                if (i < 8) {
                    // ——— GEM LAYERS ———
                    if (blacklight_exposure > 0.1 && sample.a > 0.0 && gem_emission_strengths[i] > 0.0) {
                        // Get the pure emission color from the array
                        vec3 pure_emission = gem_emission_colors[i];

                        // Use an exponential curve to soften the effect of low strengths
                        float curved_strength = pow(gem_emission_strengths[i], strength_curve);

                        // Compute a raw intensity, scaled by blacklight exposure and global emission_intensity
                        float raw_intensity = curved_strength * emission_intensity * blacklight_exposure;

                        // Optionally clamp only if > 1.0 (so normalized values remain proportional)
                        float final_intensity = clamp(raw_intensity, 0.0, 1.0);

                        // Accumulate emission: scaled by the sprite's alpha and our computed intensity
                        emission += pure_emission * sample.a * final_intensity;

                        // ALBEDO TINT — factor by final_intensity to avoid outright occluding underlying layers
                        // We interpolate between the base sample color and the fluorescent tint,
                        // weighted by how “strong” the emission is. That way a weak gem glow looks dimmer.
                        vec3 fluorescent_color = pure_emission;
                        // Blend amount = blacklight_exposure * final_intensity.
                        float blend_amount = blacklight_exposure * final_intensity;
                        vec3 mixed_color = mix(sample.rgb, fluorescent_color, blend_amount);

                        // Only composite partially, so outCol.a only reaches final_intensity (never 1 unless final_intensity=1).
                        outCol.rgb += mixed_color * sample.a * (1.0 - outCol.a) * final_intensity;
                        outCol.a += sample.a * (1.0 - outCol.a) * final_intensity;
                    }
                    // If blacklight_exposure ≤ 0.1 or no emission_strength, do nothing (gem remains invisible)
                } else {
                    // ——— NORMAL LAYERS ———
                    // Standard alpha-over blending for all non-gem layers
                    outCol.rgb += sample.rgb * sample.a * (1.0 - outCol.a);
                    outCol.a += sample.a * (1.0 - outCol.a);
                }
            }
        }
    }

    // Finally, set the built-up outputs
    ALBEDO = outCol.rgb;
    ALPHA = outCol.a;
    EMISSION = emission;
}
