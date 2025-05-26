shader_type spatial;
render_mode depth_prepass_alpha;

// Total number of layers = 27
const int LAYER_COUNT = 27;

// Per-layer uniforms (unchanged from original)
uniform sampler2D textures[LAYER_COUNT];
uniform vec4      regions[LAYER_COUNT];
uniform bool      frontFlags[LAYER_COUNT];
uniform bool      backFlags[LAYER_COUNT];

// Simple blacklight effect - just one exposure value for the whole card
uniform float blacklight_exposure : hint_range(0.0, 1.0) = 0.0;
uniform vec3  gem_emission_color : source_color = vec3(0.0, 1.0, 0.3);
uniform float gem_emission_strength : hint_range(0.0, 10.0) = 3.0;

void fragment() {
    vec2 uv = UV;
    bool isFront = uv.x < 0.5;
    // Remap to [0,1] across the card face
    float half = 0.5;
    vec2 locUV = vec2(
        isFront ? uv.x/half : (uv.x-half)/half,
        uv.y
    );
    vec4 outCol = vec4(0.0);
    vec3 emission = vec3(0.0);
    
    // Blend each layer in order (same as original)
    for (int i = 0; i < LAYER_COUNT; i++) {
        if ((isFront && frontFlags[i]) || (!isFront && backFlags[i])) {
            vec4 r = regions[i];
            if (locUV.x >= r.x && locUV.x <= r.x + r.z &&
                locUV.y >= r.y && locUV.y <= r.y + r.w) {
                vec2 sampleUV = (locUV - r.xy) / r.zw;
                vec4 sample  = texture(textures[i], sampleUV);

                // Check if this is a gem layer (last 8 layers: indices 19-26)
                if (i < 8) {
                    // Gem behavior: only visible under blacklight
                        // Make gem glow with emission
                        float glow = blacklight_exposure * gem_emission_strength;
                        emission += sample.rgb * sample.a * glow;
                        
                        vec3 fluorescent_tint = mix(sample.rgb, gem_emission_color, 0.5);
                        outCol.rgb += fluorescent_tint * sample.a * blacklight_exposure * (1.0 - outCol.a);
                        outCol.a   += sample.a * blacklight_exposure * (1.0 - outCol.a);
                    // If no blacklight, gem is invisible (skip blending)
                } 
                else
                {
                    outCol.rgb += sample.rgb * sample.a * (1.0 - outCol.a);
                    outCol.a   += sample.a   * (1.0 - outCol.a);
                }
            }
        }
    }
    ALBEDO = outCol.rgb;
    ALPHA  = outCol.a;
     EMISSION = emission;
}