shader_type spatial;
render_mode depth_prepass_alpha;

// Total number of layers = 27
const int LAYER_COUNT = 27;

// Per-layer uniforms
uniform sampler2D textures[LAYER_COUNT];
uniform vec4      regions[LAYER_COUNT];
uniform bool      frontFlags[LAYER_COUNT];
uniform bool      backFlags[LAYER_COUNT];

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
    // Blend each layer in order
    for (int i = 0; i < LAYER_COUNT; i++) {
        if ((isFront && frontFlags[i]) || (!isFront && backFlags[i])) {
            vec4 r = regions[i];
            if (locUV.x >= r.x && locUV.x <= r.x + r.z &&
                locUV.y >= r.y && locUV.y <= r.y + r.w) {
                vec2 sampleUV = (locUV - r.xy) / r.zw;
                vec4 sample  = texture(textures[i], sampleUV);
                // Alpha‐blend: simple "over" operator
                outCol.rgb += sample.rgb * sample.a * (1.0 - outCol.a);
                outCol.a   += sample.a   * (1.0 - outCol.a);
            }
        }
    }
    ALBEDO = outCol.rgb;
    ALPHA  = outCol.a;
}
