shader_type canvas_item;
render_mode unshaded;
uniform vec4 tint : source_color = vec4(0.35,0.95,1.0,1.0);
void fragment() {
    vec4 art = texture(TEXTURE,UV);
    float vein = smoothstep(0.035,0.20,art.g-art.r);
    float brightness = max(art.r,max(art.g,art.b));
    COLOR = vec4(mix(art.rgb,tint.rgb*brightness,vein),art.a);
}
