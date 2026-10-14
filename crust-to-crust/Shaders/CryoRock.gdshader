shader_type canvas_item;
render_mode unshaded;
void fragment() {
    vec4 art=texture(TEXTURE,UV)*COLOR;
    // Atlas crop: blend its outer rock into the continuous tunnel wall.
    vec2 local=(UV-vec2(388.0/1024.0,11.0/1536.0))/vec2(512.0/1024.0,1514.0/1536.0);
    float edge=smoothstep(0.0,0.10,local.y)*(1.0-smoothstep(0.89,1.0,local.y));
    edge*=1.0-smoothstep(0.65,1.0,local.x);
    COLOR=vec4(art.rgb*0.58,art.a*edge);
}
