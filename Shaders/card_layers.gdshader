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
uniform float strength_curve : hint_range(1.0, 4.0) = 1.3;

// ---------------------------------------------------------------------------
// Art-region effects, ported from Axiom2d (crates/card_game/src/shaders/*.wgsl).
// A rarity effect (embossed, glow, glossy, foil) and a condition effect (worn
// scratches, shiny shimmer) are drawn over the Art layer of the card front only:
// nowhere else on the front, and never on the back. Both default to off, so a
// card that sets nothing renders exactly as before.
//
// Axiom2d lights from the pointer. Here the light is the fixed world direction
// art_light_direction, and the viewer is the camera, both taken into the card
// face's tangent space, so tilting the card or the camera moves the highlights.
//
// Layout: constants, then one function per effect (<name>_effect), then the
// frame and dispatch helpers, then make_art_view() and art_with_effects(), the
// only two functions fragment() calls. The layer loop knows nothing else about them.
//
// Numbers that tune a look are named constants. The numbers inside the hash and
// noise functions (primes, xorshift shifts, Dave Hoskins' constants, the
// shadertoy scratch salts) are those algorithms' own and are left inline.
// ---------------------------------------------------------------------------

// Effect ids. The numbers must match RarityEffect and ConditionEffect in C#.
const int RARITY_NONE = 0;
const int RARITY_EMBOSSED = 1;
const int RARITY_GLOW = 2;
const int RARITY_GLOSSY = 3;
const int RARITY_FOIL = 4;
const int CONDITION_NONE = 0;
const int CONDITION_WORN = 1;
const int CONDITION_SHINY = 2;

// Index of the Art layer in CardTemplate.GatherAllLayers(), which fills the layer arrays.
const int ART_LAYER_INDEX = 21;

uniform int rarity_effect : hint_range(0, 4) = 0;
uniform int condition_effect : hint_range(0, 2) = 0;
// Per-card seed that places the scratches; the same seed always gives the same scratches.
uniform float art_seed = 0.0;
// Bevel map baked from the art by CardEffectNormalBaker. R, G: bevel normal x, y as 0.5 + 0.5 * n,
// B: bevel strength (1 on a region edge, 0 beyond the bevel width), A: 1 where the art is opaque.
// The transparent default has no coverage, so the rarity effects stay invisible until a map is set.
// No repeat: a wrapping, linearly filtered sample would blend the far edge's bevel into the near edge.
uniform sampler2D art_normal_map : filter_linear, repeat_disable, hint_default_transparent;
// World direction towards the light that the rarity effects shine from.
uniform vec3 art_light_direction = vec3(0.35, 0.8, 0.5);

// Shared. The turn is the value Axiom2d's shaders use, not TAU.
const float ANGLE_FULL_TURN = 6.28318;

// Embossed: the bevel catches the light. The gains and shininess are stronger than the first port's
// (0.3, 0.6, 32), which barely changed the art of a card seen head-on.
const float EMBOSS_DIFFUSE_LOW = -0.2;
const float EMBOSS_DIFFUSE_HIGH = 1.0;
const float EMBOSS_DIFFUSE_NEUTRAL = 0.5;
const float EMBOSS_DIFFUSE_GAIN = 0.9;
const float EMBOSS_SPECULAR_GAIN = 0.8;
const float EMBOSS_SHININESS = 16.0;

// Glow: glitter, one randomly tilted facet per cell of the art. Facets are flatter and the
// highlight wider than the first port's (tilt 0.45, shininess 80), so more of them flash at once.
const float GLOW_CELLS = 24.0;
const vec2 GLOW_HASH_WEIGHTS = vec2(73.0, 157.0);
const float GLOW_FACET_TILT = 0.3;
const float GLOW_SHININESS = 30.0;
const float GLOW_SPOT_INNER = 0.2;
const float GLOW_SPOT_OUTER = 0.45;
const float GLOW_MAX_ALPHA = 0.9;

// Glossy: two glints sweeping across the art, and darkened bevels.
const vec2 GLOSSY_SWEEP_A = vec2(0.6, 0.4);
const vec2 GLOSSY_SHIFT_A = vec2(0.5, 0.5);
const vec2 GLOSSY_SWEEP_B = vec2(0.3, -0.7);
const vec2 GLOSSY_SHIFT_B = vec2(-0.5, 0.5);
const float GLOSSY_SHIFT_GAIN = 0.3;
const float GLOSSY_GLINT_CENTER = 0.5;
const float GLOSSY_WIDTH_A = 12.0;
const float GLOSSY_WIDTH_B = 14.0;
const float GLOSSY_SECOND_GLINT = 0.6;
const float GLOSSY_DARKEN_MIN = 0.7;
const float GLOSSY_DARKEN_RANGE = 0.3;
const float GLOSSY_SPECULAR_GAIN = 0.9;
const float GLOSSY_ALPHA_BASE = 0.6;
const float GLOSSY_ALPHA_GAIN = 0.3;

// Foil: a metallic mirror reflecting a cosine colour field that the viewing angle sweeps across.
const float FOIL_TILT_GAIN = 0.6;
const float FOIL_UV_GAIN = 0.3;
const float FOIL_BEVEL_GAIN = 0.5;
const vec2 FOIL_RED_FREQUENCY = vec2(9.0, 4.0);
const vec2 FOIL_GREEN_FREQUENCY = vec2(4.0, -8.0);
const vec2 FOIL_BLUE_FREQUENCY = vec2(6.0, 6.0);
const float FOIL_GREEN_PHASE = 1.047;
const float FOIL_BLUE_PHASE = 2.094;
const float FOIL_ART_MIX = 0.4;
const float FOIL_ART_TINT_SCALE = 0.6;
const float FOIL_ART_TINT_FLOOR = 0.4;
const float FOIL_SHININESS = 24.0;
const float FOIL_SPECULAR_GAIN = 0.4;
const float FOIL_RIM = 0.15;
const float FOIL_ALPHA = 0.7;

// Worn: hashed scratch marks, five layers of them.
const float SCRATCH_WAVYNESS = 0.12;
const float SCRATCH_PI = 3.1415;
const float SCRATCH_WIDTH = 0.01;
const float SCRATCH_FADE_GAIN = 2.5;
const vec2 SCRATCH_CELL_HASH_X = vec2(51024.0, 3104.0);
const vec2 SCRATCH_CELL_HASH_Y = vec2(1324.0, 554.0);
const vec2 SCRATCH_SCALE = vec2(3.0, 3.0);
const int SCRATCH_LAYERS = 5;
const vec2 SCRATCH_BASE_FREQUENCY = vec2(0.5, 0.5);
const vec2 SCRATCH_FREQUENCY_STEP = vec2(0.25, 0.25);
const float SCRATCH_LAYER_TURN = 3145.0;
const vec2 SCRATCH_SEED_FREQUENCY = vec2(0.0001, 0.0002);
const vec2 SCRATCH_SEED_HASH = vec2(43758.5453, 28461.7231);
const float SCRATCH_SEED_SPREAD = 100.0;
const vec3 SCRATCH_COLOR = vec3(0.9, 0.88, 0.82);
const float SCRATCH_ALPHA = 0.7;

// Shiny: a holographic liquid shimmer pinned to the world, so moving the card sweeps it.
// Axiom2d's world is in pixels and its card is 60 px wide; this card is 0.32 m wide.
const float SHINY_PIXELS_PER_METER = 187.5;
const float SHINY_WORLD_SCALE = 0.005;
const float SHINY_LIQUID_SCALE = 0.02;
const float SHINY_VIEW_HEIGHT = 0.2;
const float SHINY_ENV_TURN = 0.8;
const float SHINY_VIGNETTE_FALLOFF = 2.0;
const float SHINY_MAX_ALPHA = 0.4;

// The liquid distortion behind the shimmer: domain-warped value noise.
const float NOISE_CELLS = 200.0;
const float LIQUID_TILT_A = 0.1;
const float LIQUID_TILT_B = 0.2;
const vec2 LIQUID_STRETCH = vec2(2.5, 0.5);
const float LIQUID_ZOOM_A = 2.5;
const float LIQUID_ZOOM_B = 6.5;
const float LIQUID_WARP_A = 0.01;
const float LIQUID_WARP_B = 0.01;
const float LIQUID_WARP_C = 0.005;
const float LIQUID_SAMPLE_SCALE = 0.1;
const float LIQUID_NORMAL_STEP = 0.0001;
const float LIQUID_NORMAL_STRENGTH = 0.001;

// The rainbow environment the shimmer reflects.
const vec3 RAINBOW_FREQUENCY = vec3(1.0, 9.0, 2.0);
const vec3 RAINBOW_PHASE = vec3(2.0, 3.0, 1.0);
const vec3 RAINBOW_TINT = vec3(0.8, 0.3, 0.7);
const float RAINBOW_LIFT = 0.2;
const float RAINBOW_SUN_SHARPNESS = 6.0;
const float RAINBOW_SUN_OFFSET = 2.0;
const float RAINBOW_SUN_GAIN = 0.05;
const float RAINBOW_GAMMA = 2.2;

// What a rarity effect needs to know about the spot it shades.
struct ArtSurface {
    vec2 uv;        // position in the art region, 0 to 1, y down
    vec3 base;      // the art colour under the effect
    vec3 normal;    // bevel normal in tangent space
    float bevel;    // 1 on a region edge, 0 beyond the bevel width
    vec3 light_dir; // tangent space, unit length
    vec3 view_dir;  // tangent space, unit length
};

// Where the light, the viewer and the fragment are, for the art effects.
struct ArtView {
    vec3 light_dir; // tangent space of the card face, unit length
    vec3 view_dir;  // tangent space of the card face, unit length
    vec3 world_pos;
};

// Screen-space derivatives of the card face that its frame is built from; read in fragment().
struct ArtDerivatives {
    vec3 dp1;   // dFdx of the view-space position
    vec3 dp2;   // dFdy of the view-space position
    vec2 duv1;  // dFdx of the position on the card face
    vec2 duv2;  // dFdy of the position on the card face
};

vec3 half_vector(ArtSurface s) {
    return normalize(s.light_dir + s.view_dir);
}

// Embossed (embossed.wgsl): a white or black tint following how the bevel faces the light.
vec4 embossed_effect(ArtSurface s) {
    float n_dot_l = dot(s.normal, s.light_dir);
    float diffuse = smoothstep(EMBOSS_DIFFUSE_LOW, EMBOSS_DIFFUSE_HIGH, n_dot_l);
    float spec = pow(max(dot(s.normal, half_vector(s)), 0.0), EMBOSS_SHININESS);
    float light_delta = (diffuse - EMBOSS_DIFFUSE_NEUTRAL) * EMBOSS_DIFFUSE_GAIN * s.bevel + spec * EMBOSS_SPECULAR_GAIN;
    if (light_delta >= 0.0) {
        return vec4(1.0, 1.0, 1.0, light_delta);
    }
    return vec4(0.0, 0.0, 0.0, -light_delta);
}

// Integer hash for the glow cells; avoids the precision problems of sin().
float glow_hash(vec2 cell) {
    uint n = uint(cell.x * GLOW_HASH_WEIGHTS.x + cell.y * GLOW_HASH_WEIGHTS.y);
    n = n ^ (n >> 13u);
    n = n * 0x5bd1e995u;
    n = n ^ (n >> 15u);
    return float(n & 0xFFFFu) / 65535.0;
}

// Glow (glow.wgsl): glitter that flashes where a cell's facet faces the half vector.
vec4 glow_effect(ArtSurface s) {
    vec2 scaled = s.uv * GLOW_CELLS;
    vec2 cell = floor(scaled);
    vec2 cell_frac = fract(scaled);

    float cell_angle = glow_hash(cell) * ANGLE_FULL_TURN;
    vec3 facet = normalize(vec3(cos(cell_angle) * GLOW_FACET_TILT, sin(cell_angle) * GLOW_FACET_TILT, 1.0));

    float spec = pow(max(dot(facet, half_vector(s)), 0.0), GLOW_SHININESS);

    // Round dots, not square cells. smoothstep(outer, inner, x) of the WGSL, written with ascending edges.
    float spot = 1.0 - smoothstep(GLOW_SPOT_INNER, GLOW_SPOT_OUTER, length(cell_frac - vec2(0.5)));
    return vec4(1.0, 1.0, 1.0, clamp(spec * spot * GLOW_MAX_ALPHA, 0.0, GLOW_MAX_ALPHA));
}

// One glossy glint: a gaussian band across the art, shifted by where the light is.
float glossy_glint(vec2 uv, vec2 light_xy, vec2 sweep, vec2 shift, float width) {
    float position = dot(uv, sweep) + dot(light_xy, shift) * GLOSSY_SHIFT_GAIN;
    float offset = (position - GLOSSY_GLINT_CENTER) * width;
    return exp(-offset * offset);
}

// Glossy (glossy.wgsl): lacquer. Two glints slide across the art as the light moves, and the
// bevel darkens the colour beneath.
vec4 glossy_effect(ArtSurface s) {
    float glint_a = glossy_glint(s.uv, s.light_dir.xy, GLOSSY_SWEEP_A, GLOSSY_SHIFT_A, GLOSSY_WIDTH_A);
    float glint_b = glossy_glint(s.uv, s.light_dir.xy, GLOSSY_SWEEP_B, GLOSSY_SHIFT_B, GLOSSY_WIDTH_B);

    float spec = max(glint_a, glint_b * GLOSSY_SECOND_GLINT);
    float inner = 1.0 - s.bevel;
    vec3 darkened = s.base * (GLOSSY_DARKEN_MIN + inner * GLOSSY_DARKEN_RANGE);
    return vec4(darkened + vec3(spec * GLOSSY_SPECULAR_GAIN), GLOSSY_ALPHA_BASE + spec * GLOSSY_ALPHA_GAIN);
}

// One channel of the foil's cosine colour field.
float foil_channel(vec2 n, vec2 frequency, float phase) {
    return cos(dot(n, frequency) + phase) * 0.5 + 0.5;
}

// Foil (foil.wgsl): the viewing angle tilts a virtual normal that looks up a colour in a
// cosine environment, plus a specular flash and a bright rim on every region edge.
vec4 foil_effect(ArtSurface s) {
    vec3 tilted = normalize(vec3(s.view_dir.xy * FOIL_TILT_GAIN, 1.0));
    vec3 grain = vec3((s.uv - vec2(0.5)) * FOIL_UV_GAIN + s.normal.xy * FOIL_BEVEL_GAIN, 0.0);
    vec3 local_n = normalize(tilted + grain);

    vec3 env_color = vec3(
        foil_channel(local_n.xy, FOIL_RED_FREQUENCY, 0.0),
        foil_channel(local_n.xy, FOIL_GREEN_FREQUENCY, FOIL_GREEN_PHASE),
        foil_channel(local_n.xy, FOIL_BLUE_FREQUENCY, FOIL_BLUE_PHASE));

    // Keep some of the art's own colour so the foil does not erase the picture.
    vec3 tinted = mix(env_color, env_color * (s.base * FOIL_ART_TINT_SCALE + FOIL_ART_TINT_FLOOR), FOIL_ART_MIX);

    float spec = pow(max(dot(local_n, half_vector(s)), 0.0), FOIL_SHININESS) * FOIL_SPECULAR_GAIN;

    float rim = s.bevel * FOIL_RIM;
    return vec4(tinted + vec3(spec) + vec3(rim), FOIL_ALPHA);
}

vec2 rotate2d(vec2 p, float angle) {
    float sa = sin(angle);
    float ca = cos(angle);
    return vec2(ca * p.x + sa * p.y, -sa * p.x + ca * p.y);
}

// One scratch in a cell (after https://www.shadertoy.com/view/4syXRD): a wavy line whose
// position and angle come from the cell's hash.
float scratch(vec2 uv_in, vec2 cell_seed) {
    float sx = floor(sin(cell_seed.x * SCRATCH_CELL_HASH_X.x) * SCRATCH_CELL_HASH_X.y);
    float sy = floor(sin(cell_seed.y * SCRATCH_CELL_HASH_Y.x) * SCRATCH_CELL_HASH_Y.y);

    vec2 uv = uv_in * 2.0 - 1.0;
    uv = rotate2d(uv, sx + sy);
    uv += sin(sx - sy);
    uv = clamp(uv * 0.5 + 0.5, vec2(0.0), vec2(1.0));

    float s1 = sin(sx + uv.y * SCRATCH_PI) * SCRATCH_WAVYNESS;
    float s2 = sin(sy + uv.y * SCRATCH_PI) * SCRATCH_WAVYNESS;

    float line = sign(SCRATCH_WIDTH - abs(uv.x - 0.5 + s2 + s1));
    return clamp(((1.0 - uv.y * uv.y) * uv.y) * SCRATCH_FADE_GAIN * line, 0.0, 1.0);
}

float scratch_layer(vec2 uv, vec2 frequency, vec2 offset, float angle) {
    vec2 scaled = rotate2d(uv, angle) * frequency + offset;
    return scratch(fract(scaled), floor(scaled));
}

// Worn (dormant.wgsl): semi-transparent scratch marks, placed from the per-card seed.
vec4 worn_effect(vec2 art_uv, float seed) {
    vec2 seed_offset = vec2(
        fract(sin(seed * SCRATCH_SEED_FREQUENCY.x) * SCRATCH_SEED_HASH.x),
        fract(sin(seed * SCRATCH_SEED_FREQUENCY.y) * SCRATCH_SEED_HASH.y)) * SCRATCH_SEED_SPREAD;

    vec2 p = art_uv * SCRATCH_SCALE + seed_offset;
    vec2 frequency = SCRATCH_BASE_FREQUENCY;
    float marks = 0.0;
    for (int i = 0; i < SCRATCH_LAYERS; i++) {
        float fi = float(i);
        marks = max(marks, scratch_layer(p, frequency, vec2(fi, fi), fi * SCRATCH_LAYER_TURN));
        frequency += SCRATCH_FREQUENCY_STEP;
    }
    return vec4(SCRATCH_COLOR, clamp(marks, 0.0, 1.0) * SCRATCH_ALPHA);
}

mat2 rot2(float a) {
    float c = cos(a);
    float s = sin(a);
    return mat2(vec2(c, s), vec2(-s, c));
}

// Dave Hoskins hash.
vec4 hash4(vec2 p_in) {
    vec4 p4 = fract(vec4(p_in.x, p_in.y, p_in.x, p_in.y) * vec4(0.1031, 0.1030, 0.0973, 0.1099));
    p4 += dot(p4, p4.wzxy + 19.19);
    return fract((p4.xxyz + p4.yzzw) * p4.zywx);
}

vec4 noise4(vec2 p_in) {
    vec2 p = p_in * NOISE_CELLS;
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(
        mix(hash4(i + vec2(0.0, 0.0)), hash4(i + vec2(1.0, 0.0)), u.x),
        mix(hash4(i + vec2(0.0, 1.0)), hash4(i + vec2(1.0, 1.0)), u.x),
        u.y);
}

// Domain-warped noise, the liquid distortion.
float liquid(vec2 p_in) {
    vec2 p = rot2(LIQUID_TILT_A) * p_in * LIQUID_STRETCH;
    p += noise4(p).rg * LIQUID_WARP_A;
    p = rot2(LIQUID_TILT_B) * p * LIQUID_ZOOM_A;
    p += noise4(p).ba * LIQUID_WARP_B;
    p *= LIQUID_ZOOM_B;
    p += noise4(p).rg * LIQUID_WARP_C;
    return noise4(p * LIQUID_SAMPLE_SCALE).a;
}

// Surface normal of the liquid by central differences. The z difference of Axiom2d's height()
// is the constant -2e (the liquid term cancels), so it is written out instead of sampled.
vec3 liquid_normal(vec2 uv) {
    float e = LIQUID_NORMAL_STEP;
    return normalize(vec3(
        (liquid(uv + vec2(e, 0.0)) - liquid(uv - vec2(e, 0.0))) * LIQUID_NORMAL_STRENGTH,
        (liquid(uv + vec2(0.0, e)) - liquid(uv - vec2(0.0, e))) * LIQUID_NORMAL_STRENGTH,
        -2.0 * e));
}

// Rainbow environment with shifted hues, for the holographic look.
vec3 rainbow_cubemap(vec3 dir) {
    vec3 color = cos(dir * RAINBOW_FREQUENCY + RAINBOW_PHASE) * 0.5 + 0.5;
    color = color * RAINBOW_TINT + vec3(RAINBOW_LIFT);
    color *= dir.y * 0.5 + 0.5;
    color += exp(RAINBOW_SUN_SHARPNESS * dir.y - RAINBOW_SUN_OFFSET) * RAINBOW_SUN_GAIN;
    return pow(color, vec3(1.0 / RAINBOW_GAMMA));
}

// Shiny (intense.wgsl): a faint holographic overlay, fading towards the art's corners.
// world_xy is the position in Axiom2d's pixel space.
vec4 shiny_effect(vec2 art_uv, vec2 world_xy) {
    vec2 wp = world_xy * SHINY_WORLD_SCALE;
    vec3 norm = liquid_normal(wp * SHINY_LIQUID_SCALE);
    vec3 dir = reflect(normalize(vec3(wp, SHINY_VIEW_HEIGHT)), norm);

    vec2 turned = rot2(SHINY_ENV_TURN) * dir.xz;
    dir = vec3(turned.x, dir.y, turned.y);

    vec3 color = clamp(rainbow_cubemap(dir), vec3(0.0), vec3(1.0));

    vec2 centered = art_uv - vec2(0.5);
    float vignette = 1.0 - dot(centered, centered) * SHINY_VIGNETTE_FALLOFF;
    return vec4(color, clamp(vignette * SHINY_MAX_ALPHA, 0.0, SHINY_MAX_ALPHA));
}

// Rotates a vector from view space into the card face's tangent space (x along u, y along v,
// z out of the face), given the face's frame as the columns of face_to_view.
vec3 view_to_face(mat3 face_to_view, vec3 v) {
    return vec3(dot(face_to_view[0], v), dot(face_to_view[1], v), dot(face_to_view[2], v));
}

// Tangent frame of the card face from screen-space derivatives, because the baked card mesh
// carries no tangents (Christian Schueler's cotangent frame).
mat3 card_face_frame(vec3 n, ArtDerivatives d) {
    vec3 dp2_perp = cross(d.dp2, n);
    vec3 dp1_perp = cross(n, d.dp1);
    vec3 t = dp2_perp * d.duv1.x + dp1_perp * d.duv2.x;
    vec3 b = dp2_perp * d.duv1.y + dp1_perp * d.duv2.y;
    float inv_max = inversesqrt(max(dot(t, t), dot(b, b)));
    return mat3(t * inv_max, b * inv_max, n);
}

// The light, the viewer and the fragment in the card face's frame. Godot built-ins are not
// visible inside functions, so fragment() passes them in together with the derivatives.
ArtView make_art_view(vec3 normal, vec3 view_pos, vec3 view_dir, ArtDerivatives derivatives,
    mat4 view_matrix, mat4 inv_view_matrix) {
    mat3 face_to_view = card_face_frame(normal, derivatives);
    vec3 light_view = (view_matrix * vec4(normalize(art_light_direction), 0.0)).xyz;
    return ArtView(
        normalize(view_to_face(face_to_view, light_view)),
        normalize(view_to_face(face_to_view, view_dir)),
        (inv_view_matrix * vec4(view_pos, 1.0)).xyz);
}

vec3 bevel_normal(vec4 relief) {
    vec2 tilt = (relief.rg - vec2(0.5)) * 2.0;
    return vec3(tilt, sqrt(max(1.0 - dot(tilt, tilt), 0.0)));
}

vec4 rarity_overlay(int effect, ArtSurface s) {
    if (effect == RARITY_EMBOSSED) {
        return embossed_effect(s);
    }
    if (effect == RARITY_GLOW) {
        return glow_effect(s);
    }
    if (effect == RARITY_GLOSSY) {
        return glossy_effect(s);
    }
    if (effect == RARITY_FOIL) {
        return foil_effect(s);
    }
    return vec4(0.0);
}

// The shimmer is pinned to the world: y feeds both axes of Axiom2d's pixel plane, so lifting
// the card moves it as well as sliding it.
vec4 condition_overlay(int effect, vec2 art_uv, vec3 world_pos) {
    if (effect == CONDITION_WORN) {
        return worn_effect(art_uv, art_seed);
    }
    if (effect == CONDITION_SHINY) {
        vec2 pixel_pos = vec2(world_pos.x + world_pos.y, world_pos.z + world_pos.y) * SHINY_PIXELS_PER_METER;
        return shiny_effect(art_uv, pixel_pos);
    }
    return vec4(0.0);
}

// The art's colour with the rarity effect and then the condition effect laid over it, as
// Axiom2d stacks its overlays. The rarity effects follow the baked bevel map and so only
// touch covered pixels; the condition effects cover the art rectangle, and show wherever
// the art is opaque.
vec3 art_with_effects(vec3 art_rgb, vec2 art_uv, ArtView art_view) {
    vec3 color = art_rgb;

    vec4 relief = textureLod(art_normal_map, art_uv, 0.0);
    if (rarity_effect != RARITY_NONE && relief.a > 0.0) {
        ArtSurface surface = ArtSurface(art_uv, color, bevel_normal(relief), relief.b, art_view.light_dir, art_view.view_dir);
        vec4 overlay = rarity_overlay(rarity_effect, surface);
        color = mix(color, overlay.rgb, clamp(overlay.a * relief.a, 0.0, 1.0));
    }

    vec4 condition = condition_overlay(condition_effect, art_uv, art_view.world_pos);
    color = mix(color, condition.rgb, condition.a);

    return clamp(color, vec3(0.0), vec3(1.0));
}

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

    // The art effects need the light, the viewer and the fragment's world position in the card
    // face's frame. They are a uniform choice, so every fragment takes the same branch.
    bool art_effects_on = rarity_effect != RARITY_NONE || condition_effect != CONDITION_NONE;
    // Screen-space derivatives are undefined outside uniform control flow, and the per-fragment
    // checks below (front face, layer, art region) are not uniform, so the derivatives are read
    // here, under the uniform test. The frame and the view are built later, for front art-layer
    // fragments only.
    ArtDerivatives art_derivatives = ArtDerivatives(vec3(0.0), vec3(0.0), vec2(0.0), vec2(0.0));
    if (art_effects_on) {
        art_derivatives = ArtDerivatives(dFdx(VERTEX), dFdy(VERTEX), dFdx(locUV), dFdy(locUV));
    }

    // Blend each layer in order
    for (int i = 0; i < LAYER_COUNT; i++) {
        if ((isFront && frontFlags[i]) || (!isFront && backFlags[i])) {
            vec4 r = regions[i];
            if (locUV.x >= r.x && locUV.x <= r.x + r.z &&
                locUV.y >= r.y && locUV.y <= r.y + r.w) {
                vec2 sampleUV = (locUV - r.xy) / r.zw;
                vec4 sample = texture(textures[i], sampleUV);
                if (i == ART_LAYER_INDEX && isFront && art_effects_on) {
                    ArtView art_view = make_art_view(normalize(NORMAL), VERTEX, VIEW, art_derivatives,
                        VIEW_MATRIX, INV_VIEW_MATRIX);
                    sample.rgb = art_with_effects(sample.rgb, sampleUV, art_view);
                }

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
