shader_type canvas_item;
render_mode unshaded;
uniform float clock=0.0;
uniform sampler2D night_texture : source_color,filter_linear,repeat_disable;
float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
void fragment() {
    vec3 sky=mix(vec3(0.008,0.017,0.038),vec3(0.09,0.16,0.20),pow(UV.y,4.0));
    vec2 cells=UV*vec2(800.0,360.0);
    float star=step(0.993,hash(floor(cells)))*(1.0-smoothstep(0.0,0.17,length(fract(cells)-0.5)));
    float shimmer=0.7+0.15*sin(clock*0.6+hash(floor(cells))*12.0);
    sky+=star*shimmer*vec3(0.65,0.75,0.83);
    float cloud=sin(UV.x*34.0+UV.y*9.0)*sin(UV.x*19.0-UV.y*17.0);
    sky+=smoothstep(0.4,0.95,cloud)*0.012*UV.y;
    vec2 point=UV*vec2(10000.0,4550.0)-vec2(5000.0);
    vec2 photo_uv=vec2(point.x/1800.0+0.5,(point.y+1250.0)/900.0);
    float mask=smoothstep(0.0,0.12,photo_uv.y)*(1.0-smoothstep(0.78,1.0,abs(photo_uv.x*2.0-1.0)));
    vec3 panorama=texture(night_texture,clamp(photo_uv,vec2(0.0),vec2(1.0))).rgb*0.72;
    COLOR=vec4(mix(sky,panorama,mask),1.0);
}
