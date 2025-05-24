 shader_type spatial; render_mode unshaded, cull_disabled;
// Textures
uniform sampler2D back_texture;
uniform sampler2D front_texture;
uniform sampler2D border_texture;
uniform sampler2D art_texture;
uniform sampler2D symbol_texture;
uniform sampler2D name_texture;
uniform sampler2D attribute_box_texture;
uniform sampler2D attribute_text_texture;
uniform sampler2D emissive_mask;

// Region definitions: (x_min, y_min, x_max, y_max)
uniform vec4 border_region;
uniform vec4 art_region;
uniform vec4 symbol_region;
uniform vec4 name_region;
uniform vec4 attribute_region;
uniform vec4 attribute_text_region;

// Blacklight toggle: 0 = off, 1 = on
uniform float blacklight_enabled : hint_range(0.0, 1.0);

void fragment() {
vec2 uv = UV;
vec4 out_color;

// Show back design when the card is flipped
if (NORMAL.z < 0.0) {
    out_color = texture(back_texture, uv);
} else {
    // Base front design
    out_color = texture(front_texture, uv);

    // Border (9-slice) overlay
    if (uv.x >= border_region.x && uv.x <= border_region.z
     && uv.y >= border_region.y && uv.y <= border_region.w) {
        vec2 sub = (uv - border_region.xy) / (border_region.zw - border_region.xy);
        out_color = texture(border_texture, sub);
    }
    // Card art
    else if (uv.x >= art_region.x && uv.x <= art_region.z
          && uv.y >= art_region.y && uv.y <= art_region.w) {
        vec2 sub = (uv - art_region.xy) / (art_region.zw - art_region.xy);
        out_color = texture(art_texture, sub);
    }
    // Set symbol (top-left)
    else if (uv.x >= symbol_region.x && uv.x <= symbol_region.z
          && uv.y >= symbol_region.y && uv.y <= symbol_region.w) {
        vec2 sub = (uv - symbol_region.xy) / (symbol_region.zw - symbol_region.xy);
        out_color = texture(symbol_texture, sub);
    }
    // Card name text
    else if (uv.x >= name_region.x && uv.x <= name_region.z
          && uv.y >= name_region.y && uv.y <= name_region.w) {
        vec2 sub = (uv - name_region.xy) / (name_region.zw - name_region.xy);
        out_color = texture(name_texture, sub);
    }
    // Attribute box background
    else if (uv.x >= attribute_region.x && uv.x <= attribute_region.z
          && uv.y >= attribute_region.y && uv.y <= attribute_region.w) {
        vec2 sub = (uv - attribute_region.xy) / (attribute_region.zw - attribute_region.xy);
        out_color = texture(attribute_box_texture, sub);
    }
    // Attribute text
    else if (uv.x >= attribute_text_region.x && uv.x <= attribute_text_region.z
          && uv.y >= attribute_text_region.y && uv.y <= attribute_text_region.w) {
        vec2 sub = (uv - attribute_text_region.xy) / (attribute_text_region.zw - attribute_text_region.xy);
        out_color = texture(attribute_text_texture, sub);
    }
}

// Emissive blacklight mask
float mask = texture(emissive_mask, uv).r;
EMISSION = vec3(mask * blacklight_enabled);

// Final output
ALBEDO = out_color.rgb;
ALPHA  = out_color.a;
}