shader_type canvas_item;
render_mode unshaded;
uniform float warmth : hint_range(0.0, 1.0) = 0.0;
uniform float clock = 0.0;
uniform float flash_intensity = 1.0;
uniform float zone_glow = 0.0;
varying vec4 instance_tint;
void vertex() { instance_tint = COLOR; }
void fragment() {
    vec4 crystal = texture(TEXTURE, UV);
    float vein = smoothstep(0.025, 0.22, crystal.b - crystal.r);
    vec3 amber = vec3(max(crystal.r,crystal.b)*1.14, crystal.g*0.60, crystal.r*0.32);
    vec3 art = mix(crystal.rgb, amber, warmth * vein);
    float pulse = mix(0.85,1.08,zone_glow) + flash_intensity * 0.035 * sin(clock * 1.7);
    // Weak mineral veins remain orienting cues, rather than lighting the whole tunnel.
    COLOR = vec4(art * pulse * (0.10 + vein * 0.24), crystal.a) * instance_tint;
}
