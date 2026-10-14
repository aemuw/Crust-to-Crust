shader_type canvas_item;
uniform sampler2D hot_texture : source_color, filter_linear, repeat_enable;
uniform float warmth = 0.0;
uniform vec2 uv_offset = vec2(0.0);
void fragment() {
    vec2 uv = UV + uv_offset;
    vec3 cold = texture(TEXTURE,uv).rgb * vec3(0.35,0.42,0.55);
    vec3 hot = texture(hot_texture,uv).rgb * vec3(0.68,0.32,0.15);
    COLOR = vec4(mix(cold,hot,pow(warmth,1.4)),1.0);
}
