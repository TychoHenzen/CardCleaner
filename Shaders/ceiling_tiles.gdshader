// A suspended shop ceiling: square tiles held by a thin metal grid. The grid follows world space, so the
// ceiling of every room lines up and a resized slab needs no new UVs.
shader_type spatial;

uniform vec4 tile_colour : source_color = vec4(0.9, 0.9, 0.88, 1.0);
uniform vec4 grid_colour : source_color = vec4(0.58, 0.6, 0.63, 1.0);
uniform float tile_size = 0.6;
uniform float grid_width = 0.03;
// How much one tile may differ in brightness from the next, so the ceiling does not read as one flat plane.
uniform float tile_shade = 0.035;

varying vec3 world_position;

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec2 cell = world_position.xz / tile_size;
    vec2 from_centre = abs(fract(cell) - 0.5) * tile_size;
    float on_grid = step(tile_size * 0.5 - grid_width * 0.5, max(from_centre.x, from_centre.y));
    float shade = fract(sin(dot(floor(cell), vec2(12.9898, 78.233))) * 43758.5453) * tile_shade;
    ALBEDO = mix(tile_colour.rgb - shade, grid_colour.rgb, on_grid);
    ROUGHNESS = mix(0.95, 0.4, on_grid);
    METALLIC = mix(0.0, 0.5, on_grid);
}
