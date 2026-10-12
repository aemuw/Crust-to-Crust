shader_type canvas_item;
render_mode unshaded;
varying vec2 frame_position;
void vertex() { frame_position = VERTEX; }
void fragment() {
    vec4 water = COLOR;
    water.a *= 1.0-smoothstep(135.0,255.0,frame_position.y);
    water.a *= 1.0-smoothstep(110.0,127.0,abs(frame_position.x));
    COLOR = water;
}
