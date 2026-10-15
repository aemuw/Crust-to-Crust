shader_type canvas_item;
render_mode unshaded;
uniform float clock=0.0;
uniform float flow=0.0;
float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
float noise(vec2 p) {
    vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
void fragment() {
    // These exact outer edges are shared by MagmaVent.PlumePolygon.
    float center=0.6666667-UV.x*0.2916667;
    float width=0.1+UV.x*0.1833333;
    float distance=abs(UV.y-center)/width;
    float turbulence=noise(vec2(UV.x*12.0-clock*4.0,UV.y*14.0+clock));
    float wisps=noise(vec2(UV.x*23.0-clock*7.0,UV.y*25.0));
    float edge=1.0-smoothstep(0.15+0.25*turbulence,1.0,distance+0.22*(1.0-wisps));
    float tip=1.0-smoothstep(0.55,1.0,UV.x);
    vec3 tint=mix(vec3(0.29,0.24,0.21),vec3(0.88,0.48,0.20),turbulence*0.65);
    COLOR=vec4(tint,edge*tip*(0.2+0.65*turbulence*wisps)*flow);
}
