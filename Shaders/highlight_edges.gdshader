// The aim highlight of an interactable: a box drawn as glowing edges with a faint fill, so the object inside stays
// readable. It is meant for a BoxMesh, whose UVs lay the six faces out on a 3 x 2 grid: each face's own 0..1
// coordinates come from the cell, and the edge lines keep the same width in pixels at any distance.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_back, shadows_disabled;

uniform vec4 edge_colour : source_color = vec4(1.0, 0.86, 0.35, 1.0);
uniform float edge_pixels = 2.0;
uniform float fill_alpha = 0.04;
uniform float pulse_speed = 3.0;

void fragment() {
	vec2 face = fract(UV * vec2(3.0, 2.0));
	vec2 to_edge = min(face, 1.0 - face) / max(fwidth(face), vec2(1e-5));
	float edge = 1.0 - smoothstep(edge_pixels - 0.5, edge_pixels + 0.5, min(to_edge.x, to_edge.y));
	float pulse = 0.8 + 0.2 * sin(TIME * pulse_speed);
	ALBEDO = edge_colour.rgb;
	ALPHA = max(edge * edge_colour.a * pulse, fill_alpha);
}
