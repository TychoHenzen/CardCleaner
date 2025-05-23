shader_type spatial;
render_mode cull_back;

uniform sampler2D textureFrontArt;
uniform sampler2D textureBorder;
uniform sampler2D textureSetSymbol;
uniform sampler2D textureBackDesign;
uniform sampler2D textureEmissive;

uniform bool is_shiny = false;
uniform vec4 set_symbol_uv = vec4(0.8, 0.8, 0.15, 0.15); // x,y=offset; z,w=size

void fragment() {
    vec2 uv = UV;
    // FRONT vs BACK by normal.z
    if (FRONT_FACING) {
        // base art + border
        vec4 art  = texture(textureFrontArt, uv);
        vec4 bord = texture(textureBorder,    uv);
        ALBEDO = mix(art.rgb, bord.rgb, bord.a);
        ALPHA  = art.a + bord.a * (1.0 - art.a);
        // set symbol overlay
        vec2 rel = (uv - set_symbol_uv.xy) / set_symbol_uv.zw;
        if (all(greaterThanEqual(rel, vec2(0))) && all(lessThanEqual(rel, vec2(1)))) {
            vec4 sym = texture(textureSetSymbol, rel);
            ALBEDO = mix(ALBEDO, sym.rgb, sym.a);
            ALPHA  = max(ALPHA, sym.a);
        }
    } else {
        ALBEDO = texture(textureBackDesign, uv).rgb;
        ALPHA  = 1.0;
    }
    // shiny control
    if (is_shiny) {
        METALLIC  = 1.0;
        ROUGHNESS = 0.1;
    }
    // emissive (blacklight)
    EMISSION = texture(textureEmissive, uv).rgb;
}
