shader_type canvas_item;
uniform sampler2D hot_texture : source_color, filter_linear, repeat_enable;
uniform float warmth : hint_range(0.0, 1.0) = 0.0;
uniform vec2 texture_origin = vec2(0.0);
uniform vec2 texture_period = vec2(960.0, 1440.0);
uniform int edge_mode = 0;
varying vec2 rock_position;
varying vec4 rock_tint;
void vertex() { rock_position = VERTEX; rock_tint = COLOR; }
float edge(float y, bool left) {
    float t = clamp(y / 120.0, 0.0, 5.999);
    float f = fract(t);
    if (t < 1.0) return left ? mix(320.0,290.0,f) : mix(320.0,292.0,f);
    if (t < 2.0) return left ? mix(290.0,265.0,f) : mix(292.0,285.0,f);
    if (t < 3.0) return left ? mix(265.0,280.0,f) : mix(285.0,275.0,f);
    if (t < 4.0) return left ? mix(280.0,275.0,f) : mix(275.0,270.0,f);
    if (t < 5.0) return left ? mix(275.0,295.0,f) : mix(270.0,285.0,f);
    return left ? mix(295.0,320.0,f) : mix(285.0,320.0,f);
}
void fragment() {
    // Mirrored sampling is continuous even for painted, non-tileable source art.
    // All slabs share logical cave coordinates, including across recycled sections.
    vec2 uv = abs(fract((rock_position + texture_origin) / texture_period * 0.5) * 2.0 - 1.0);
    vec4 cold = texture(TEXTURE, uv);
    vec4 hot = texture(hot_texture, uv);
    float feather = 1.0;
    float blur = 0.0;
    if (edge_mode != 0) {
        float d = abs(rock_position.x) - edge(rock_position.y, edge_mode == 1);
        feather = smoothstep(0.0, 16.0, d);
        if (d < 34.0) {
            blur = (1.0-smoothstep(0.0,34.0,d))*0.7;
            vec2 px = 2.0/vec2(textureSize(TEXTURE,0));
            cold = mix(cold,texture(TEXTURE,uv+px),blur);
        }
    }
    vec4 stone = mix(cold, hot, warmth);
    // Preserve native Godot lighting; the lava-painted cracks acquire a warm tint.
    COLOR = vec4(stone.rgb, stone.a * feather) * rock_tint;
}
