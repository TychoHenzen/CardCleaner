shader_type spatial;
render_mode cull_front;

uniform float width = 0.05;
uniform vec4 outline_color = vec4(1.0,1.0,1.0,1.0);

void vertex() {
    VERTEX += NORMAL * width;
}

void fragment() {
    ALBEDO = outline_color.rgb;
    ALPHA = outline_color.a;
}
