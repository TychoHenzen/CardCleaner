// The foil wrapper of a booster (mesh from ContainerMeshRecipe.BoosterBody and BoosterTearStrip). The front print
// lies on the left half of the UVs and the back print on the right half; v runs from the torn end (0) to the bottom
// crimp (1). The vertex colour's red channel marks the crimped seals, which are plain ridged foil.
shader_type spatial;
render_mode cull_disabled;

uniform sampler2D art_texture : source_color, filter_linear_mipmap, repeat_disable;
// The art window on the front as (left, top, right, bottom) in front-print UVs, and the part of the art it shows.
uniform vec4 art_window = vec4(0.1, 0.29, 0.9, 0.7);
uniform vec4 art_crop = vec4(0.0, 0.228, 1.0, 0.712);
uniform vec4 foil_colour : source_color = vec4(0.11, 0.12, 0.2, 1.0);
uniform vec4 band_colour : source_color = vec4(0.85, 0.2, 0.24, 1.0);
uniform vec4 trim_colour : source_color = vec4(0.91, 0.7, 0.54, 1.0);
uniform vec4 seal_colour : source_color = vec4(0.72, 0.74, 0.78, 1.0);
// How strongly bare foil throws a rainbow at grazing angles.
uniform float sheen = 0.35;
// Crimp ridges per metre along the booster's length.
uniform float ridges_per_metre = 700.0;

varying vec3 local_position;

void vertex() {
    local_position = VERTEX;
}

float inside_rect(vec2 p, vec4 r) {
    return step(r.x, p.x) * step(p.x, r.z) * step(r.y, p.y) * step(p.y, r.w);
}

float frame_of(vec2 p, vec4 r, float width) {
    return inside_rect(p, r + vec4(-width, -width, width, width)) * (1.0 - inside_rect(p, r));
}

vec3 rainbow(float t) {
    return 0.5 + 0.5 * cos(6.28318 * (t + vec3(0.0, 0.33, 0.67)));
}

void fragment() {
    bool front = UV.x < 0.5;
    vec2 p = vec2(front ? UV.x * 2.0 : (UV.x - 0.5) * 2.0, UV.y);
    float seal = step(0.5, COLOR.r);
    vec3 colour = foil_colour.rgb;
    // Ink covers the foil where something is printed; bare foil is metallic and shows the sheen.
    float ink = 0.0;

    if (front) {
        float stripe = step(0.04, p.x) * step(p.x, 0.06) + step(0.94, p.x) * step(p.x, 0.96);
        colour = mix(colour, trim_colour.rgb, stripe);
        float band = step(0.72, p.y) * step(p.y, 0.81);
        colour = mix(colour, band_colour.rgb, band);
        float frame = frame_of(p, art_window, 0.012);
        colour = mix(colour, trim_colour.rgb, frame);
        float window = inside_rect(p, art_window);
        vec2 art_uv = mix(art_crop.xy, art_crop.zw, (p - art_window.xy) / (art_window.zw - art_window.xy));
        colour = mix(colour, texture(art_texture, art_uv).rgb, window);
        ink = clamp(window + band + frame + stripe, 0.0, 1.0);
    } else {
        float panel = inside_rect(p, vec4(0.12, 0.2, 0.88, 0.8));
        colour = mix(colour, mix(foil_colour.rgb, trim_colour.rgb, 0.3), panel);
        float fin = step(0.47, p.x) * step(p.x, 0.53);
        colour = mix(colour, seal_colour.rgb * 0.75, fin);
        ink = panel * (1.0 - fin);
    }

    colour = mix(colour, seal_colour.rgb, seal);
    ink *= 1.0 - seal;
    float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
    colour += rainbow(facing * 1.5 + p.y) * sheen * (1.0 - facing) * (1.0 - ink);

    ALBEDO = colour;
    METALLIC = mix(0.85, 0.05, ink);
    ROUGHNESS = mix(0.26, 0.45, ink);
    float ridge = cos(local_position.z * ridges_per_metre * 6.28318) * seal;
    NORMAL_MAP = vec3(0.5, 0.5 + 0.3 * ridge, 1.0);
}
