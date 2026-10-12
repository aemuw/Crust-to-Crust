shader_type canvas_item;
render_mode unshaded;
uniform float clock = 0.0;
varying vec2 local_uv;
void vertex() { local_uv = UV; }
void fragment() {
    vec4 base = texture(TEXTURE,UV);
    // Move only water-colored pixels; the painted source ledge remains anchored.
    float water_mask = smoothstep(0.06,0.22,base.b-base.r)*smoothstep(0.10,0.20,UV.y);
    float ripple = sin(UV.y*58.0-clock*6.0)*0.0025 + sin(UV.y*93.0-clock*9.0)*0.0015;
    vec4 flow = texture(TEXTURE,clamp(UV+vec2(ripple,0.0),vec2(0.001),vec2(0.999)));
    vec4 art = mix(base,flow,water_mask);
    float shimmer = 1.0+0.06*sin(UV.y*66.0-clock*8.0)*water_mask;
    art.rgb *= shimmer;
    art.a *= 1.0-smoothstep(0.82,0.99,UV.y);
    COLOR = art;
}
