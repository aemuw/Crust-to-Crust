shader_type canvas_item;
render_mode unshaded;
uniform float clock = 0.0;
uniform float strength : hint_range(0.0,1.0) = 1.0;
uniform float wind : hint_range(-1.0,1.0) = 0.0;
uniform float speed : hint_range(0.0,1.0) = 0.0;
float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
float noise(vec2 p) {
    vec2 i = floor(p), f = fract(p); f = f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
float fbm(vec2 p) { return noise(p)*0.57+noise(p*2.1)*0.28+noise(p*4.3)*0.15; }
void fragment() {
    vec2 p = UV;
    // Base of the flame encloses the sprite; tips trail upwards against the fall.
    float height = clamp((0.84-p.y)/0.80,0.0,1.0);
    float curl = (fbm(vec2(p.x*5.0,p.y*6.0+clock*2.5))-0.5)*0.16*height;
    float center = 0.5 + wind*height*0.17 + curl;
    float width = mix(0.32,0.025,pow(height,0.8));
    float body = 1.0-smoothstep(width*0.35,width,abs(p.x-center));
    float n = fbm(vec2(p.x*9.0,p.y*10.0+clock*(3.0+speed*2.0)));
    float tongue = smoothstep(0.22,0.65,n+(1.0-height)*0.35);
    float alpha = body*tongue*(1.0-smoothstep(0.77,0.96,p.y))*smoothstep(0.0,0.11,p.y)*strength;
    float core = clamp(body*(1.0-height)*1.6+n*0.3,0.0,1.0);
    vec3 fire = mix(vec3(0.85,0.055,0.008),vec3(1.0,0.48,0.045),smoothstep(0.1,0.75,core));
    fire = mix(fire,vec3(1.0,0.94,0.62),smoothstep(0.72,1.0,core));
    COLOR = vec4(fire,alpha*0.92);
}
