// Asset preparation only: split the imagegen sheets by alpha and export native
// low-resolution sprites. Usage: NODE_PATH=<sharp location> node this.cjs <sheets-dir>
const sharp = require('sharp');
const fs = require('node:fs');
const path = require('node:path');
const sources = [
  ['exec-ca1abb60-8d5d-40cc-9fe6-59f43a07628a.png', ['hero_basalt','hero_ice','hero_ember','cube_copper','cube_amethyst']],
  ['exec-f46cd353-e40d-401c-94bf-2dc577d1ce04.png', ['cube_magma','circle_obsidian','circle_copper','circle_opal']],
  ['exec-79ea221a-c56f-484f-986c-5b7df782f39b.png', ['triangle_basalt','triangle_amber','triangle_ice','bar_copper','bar_jade']],
  ['exec-7fb0fc02-732c-43cb-b879-d70a214c3c65.png', ['diamond_cobalt','diamond_garnet','hex_bronze','hex_malachite']],
  ['exec-3c6f0e21-604d-4feb-a375-38b845e33c63.png', ['pentagon_basalt','pentagon_amethyst','pentagon_copper','octagon_magma','octagon_ice','octagon_opal','trapezoid_basalt','trapezoid_jade','trapezoid_copper']],
];
async function main() {
  if (!process.argv[2]) throw Error('Provide the directory containing the five original imagegen sheets');
  const output=path.resolve(__dirname,'../Textures/PixelSkins'); fs.mkdirSync(output,{recursive:true});
  for (const [filename,names] of sources) {
    const {data,info}=await sharp(path.join(process.argv[2],filename)).ensureAlpha().raw().toBuffer({resolveWithObject:true});
    const {width,height}=info, seen=new Uint8Array(width*height), queue=new Int32Array(width*height), components=[];
    for (let p=0;p<seen.length;p++) {
      if(seen[p] || data[p*4+3]<128) continue;
      let start=0,end=1,left=width,top=height,right=0,bottom=0; queue[0]=p; seen[p]=1;
      while(start<end) {
        const at=queue[start++], x=at%width,y=Math.floor(at/width);
        left=Math.min(left,x); right=Math.max(right,x); top=Math.min(top,y); bottom=Math.max(bottom,y);
        for(const n of [x>0 ? at-1 : -1,x<width-1 ? at+1 : -1,y>0 ? at-width : -1,y<height-1 ? at+width : -1])
          if(n>=0 && !seen[n] && data[n*4+3]>=128) { seen[n]=1; queue[end++]=n; }
      }
      if(end>5000) components.push({left,top,width:right-left+1,height:bottom-top+1,area:end});
    }
    // The three triangle bases touch in this sheet; isolate their cells before
    // measuring alpha so the shared baseline cannot merge different skins.
    if(names[0]==='triangle_basalt') {
      components.length=0;
      const cuts=[0,409,760,1139,1650,width];
      for(let cell=0;cell<5;cell++) {
        let left=width,top=height,right=-1,bottom=-1;
        for(let y=0;y<height;y++) for(let x=cuts[cell];x<cuts[cell+1];x++)
          if(data[(y*width+x)*4+3]>=128) {left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);}
        components.push({left,top,width:right-left+1,height:bottom-top+1});
      }
    }
    components.sort((a,b)=>names.length===9 && Math.abs(a.top-b.top)>150 ? a.top-b.top : a.left-b.left);
    if(components.length!==names.length) throw Error(`${filename}: expected ${names.length} sprites, found ${components.length}: ${JSON.stringify(components)}`);
    for(let i=0;i<names.length;i++) {
      const {left,top,width:w,height:h}=components[i];
      const resized=await sharp(data,{raw:info}).extract({left,top,width:w,height:h})
        .resize({width:48,height:48,fit:'inside',kernel:'nearest'}).raw().toBuffer({resolveWithObject:true});
      for(let j=3;j<resized.data.length;j+=4) resized.data[j]=resized.data[j]>=128 ? 255 : 0;
      await sharp(resized.data,{raw:resized.info}).extend({top:1,bottom:1,left:1,right:1,background:{r:0,g:0,b:0,alpha:0}})
        .png({palette:true,colours:32,dither:0}).toFile(path.join(output,names[i]+'.png'));
      console.log(`${names[i]}: ${resized.info.width}x${resized.info.height}`);
    }
  }
}
main().catch(error=>{console.error(error);process.exitCode=1;});
