shader_type canvas_item;
varying float sprite_y;
void vertex() {
    sprite_y = VERTEX.y;
}
void fragment() {
    vec4 water = COLOR;
    // Frame-local height keeps the fade identical in every atlas frame.
    water.a *= 1.0 - smoothstep(100.0, 252.0, sprite_y);
    COLOR = water;
}
