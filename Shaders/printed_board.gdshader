// A printed retail box or a plain kraft shipping carton (meshes from ContainerMeshRecipe). The vertex colour's red
// channel names the face, as ContainerMeshRecipe.PrintFace does: 0 top, 1 front, 2 back, 3 side, 4 bottom, 5 inside,
// 6 cut edge. Every face has its own 0..1 UVs, with u to the right and v down the print.
shader_type spatial;

// A kraft shipping carton instead of a printed box.
uniform bool corrugated = false;
uniform vec4 board_colour : source_color = vec4(0.11, 0.12, 0.2, 1.0);
uniform vec4 accent_colour : source_color = vec4(0.91, 0.7, 0.54, 1.0);
uniform vec4 band_colour : source_color = vec4(0.85, 0.2, 0.24, 1.0);
uniform vec4 inside_colour : source_color = vec4(0.78, 0.74, 0.66, 1.0);
uniform vec4 edge_colour : source_color = vec4(0.62, 0.5, 0.36, 1.0);
uniform sampler2D art_texture : source_color, filter_linear_mipmap, repeat_disable;
uniform bool has_art = false;
// The art window on the front as (left, top, right, bottom) in face UVs, and the part of the art it shows.
uniform vec4 art_window = vec4(0.12, 0.2, 0.88, 0.8);
uniform vec4 art_crop = vec4(0.0, 0.2, 1.0, 0.75);
// Width over depth of the top face, so its roundel stays round.
uniform float top_aspect = 1.0;
// Packing tape: on a flap's top from tape_from to its free edge (u = 1), and down the middle of the front and back.
uniform bool taped = false;
uniform vec4 tape_colour : source_color = vec4(0.76, 0.6, 0.38, 1.0);
uniform float tape_from = 0.86;
uniform float tape_half_width = 0.075;
uniform float tape_drop = 0.25;
// Corrugation flutes per metre along a cut edge.
uniform float flutes_per_metre = 250.0;

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

float hash(vec3 p) {
    return fract(sin(dot(floor(p), vec3(12.9898, 78.233, 37.719))) * 43758.5453);
}

// Paper fibre: blotchy value noise at two scales, centred on zero.
float fibre(vec3 p) {
    return (hash(p * 180.0) - 0.5) * 0.05 + (hash(p * 35.0) - 0.5) * 0.06;
}

vec3 printed(int face, vec2 uv) {
    vec3 colour = board_colour.rgb;
    if (face == 1) {
        colour = mix(colour, accent_colour.rgb, frame_of(uv, vec4(0.04, 0.04, 0.96, 0.96), 0.012));
        colour = mix(colour, band_colour.rgb, step(0.84, uv.y) * step(uv.y, 0.94));
        if (has_art) {
            colour = mix(colour, accent_colour.rgb, frame_of(uv, art_window, 0.015));
            vec2 art_uv = mix(art_crop.xy, art_crop.zw, (uv - art_window.xy) / (art_window.zw - art_window.xy));
            colour = mix(colour, texture(art_texture, art_uv).rgb, inside_rect(uv, art_window));
        }
    } else if (face == 2) {
        colour = mix(colour, mix(board_colour.rgb, accent_colour.rgb, 0.3), inside_rect(uv, vec4(0.1, 0.12, 0.9, 0.7)));
        colour = mix(colour, band_colour.rgb, step(0.8, uv.y) * step(uv.y, 0.9));
    } else if (face == 3) {
        float stripes = step(0.5, fract((uv.x + uv.y * 0.6) * 5.0)) * step(0.62, uv.y);
        colour = mix(colour, mix(band_colour.rgb, accent_colour.rgb, step(0.5, fract((uv.x + uv.y * 0.6) * 2.5))),
            stripes);
    } else if (face == 0) {
        float d = length((uv - 0.5) * vec2(top_aspect, 1.0));
        colour = mix(colour, accent_colour.rgb, step(d, 0.3) * step(0.24, d));
        colour = mix(colour, band_colour.rgb, step(d, 0.2));
    } else if (face == 4) {
        colour *= 0.7;
    }
    return colour;
}

float tape_on(int face, vec2 uv) {
    if (!taped) {
        return 0.0;
    }
    if (face == 0) {
        return step(tape_from, uv.x);
    }
    if (face == 1 || face == 2) {
        return step(abs(uv.x - 0.5), tape_half_width) * step(uv.y, tape_drop);
    }
    return 0.0;
}

void fragment() {
    int face = int(floor(COLOR.r * 8.0));
    vec3 colour;
    float rough;
    if (face == 6) {
        float flute = 0.5 + 0.5 * sin((local_position.x + local_position.z) * flutes_per_metre * 6.28318);
        colour = edge_colour.rgb * mix(0.75, 1.05, flute);
        rough = 0.95;
    } else if (face == 5) {
        colour = inside_colour.rgb + fibre(local_position);
        rough = 0.95;
    } else if (corrugated) {
        colour = board_colour.rgb + fibre(local_position);
        rough = 0.9;
    } else {
        colour = printed(face, UV);
        rough = 0.38;
    }

    float tape = tape_on(face, UV);
    colour = mix(colour, tape_colour.rgb, tape);
    ALBEDO = colour;
    ROUGHNESS = mix(rough, 0.25, tape);
    SPECULAR = mix(corrugated ? 0.3 : 0.5, 0.6, tape);
}
