shader_type canvas_item;
render_mode unshaded;
uniform float warmth : hint_range(0.0, 1.0) = 0.0;
uniform float clock = 0.0;
void fragment() {
    vec4 crystal = texture(TEXTURE, UV);
    float vein = smoothstep(0.025, 0.22, crystal.b - crystal.r);
    vec3 amber = vec3(max(crystal.r,crystal.b)*1.14, crystal.g*0.60, crystal.r*0.32);
    vec3 art = mix(crystal.rgb, amber, warmth * vein);
    float pulse = 1.0 + 0.035 * sin(clock * 1.7);
    COLOR = vec4(art * pulse, crystal.a);
}
