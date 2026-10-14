shader_type canvas_item;
render_mode unshaded;
uniform float clock = 0.0;
uniform float flow = 0.0;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1,311.7)))*43758.5453); }
float noise(vec2 p) {
    vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
void fragment() {
    float x=UV.x;
    float center=0.8333-x*0.3333;
    float curl=sin(x*16.0-clock*3.5)*0.018*x;
    float dist=abs(UV.y-center-curl);
    float width=mix(0.09,0.19,x);
    float edge=1.0-smoothstep(width*0.3,width,dist);
    float n=noise(vec2(x*9.0-clock*2.0,UV.y*17.0));
    float mist=edge*(0.28+0.72*n)*(1.0-smoothstep(0.7,1.0,x));
    float wisp=pow(max(0.0,1.0-dist/(width*0.5)),3.0)*(0.6+0.4*sin(x*26.0-clock*6.0));
    COLOR=vec4(mix(vec3(0.35,0.63,0.72),vec3(0.78,0.92,0.95),n),clamp((mist*0.46+wisp*0.15)*flow,0.0,0.7));
}
