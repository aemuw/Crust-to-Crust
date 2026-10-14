shader_type canvas_item;
uniform sampler2D hot_texture : source_color, filter_linear, repeat_enable;
uniform float warmth = 0.0;
uniform vec2 texture_origin = vec2(0.0);
uniform vec2 texture_period = vec2(960.0, 1800.0);
varying vec2 cave_position;
void vertex() { cave_position = VERTEX; }
void fragment() {
    vec2 uv = abs(fract((cave_position + texture_origin) / texture_period * 0.5) * 2.0 - 1.0);
    vec3 cold = texture(TEXTURE,uv).rgb * vec3(0.18,0.23,0.30);
    vec3 hot = texture(hot_texture,uv).rgb * vec3(0.32,0.14,0.065);
    COLOR = vec4(mix(cold,hot,pow(warmth,1.4)),1.0);
}
