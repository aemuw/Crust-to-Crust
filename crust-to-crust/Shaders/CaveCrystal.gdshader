shader_type canvas_item;
uniform float saturation : hint_range(0.0, 1.0) = 0.32;
uniform float brightness : hint_range(0.0, 1.0) = 0.70;
void fragment() {
    vec4 crystal = COLOR;
    float luminance = dot(crystal.rgb, vec3(0.299, 0.587, 0.114));
    vec3 muted = mix(vec3(luminance), crystal.rgb, saturation) * brightness;
    COLOR = vec4(muted, crystal.a);
}
