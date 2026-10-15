"""Original tectonic preparation cue; deterministic local synthesis without external services."""
from pathlib import Path
import numpy as np
import wave
rate=44100
t=np.arange(int(rate*1.9))/rate
rng=np.random.default_rng(8102026)
f=np.fft.rfftfreq(len(t),1/rate)
noise=np.fft.irfft(np.fft.rfft(rng.normal(0,1,len(t)))/(1+(f/1800)**2)*f/(f+100),n=len(t))
noise/=max(abs(noise))
env=np.minimum(np.clip(t/.1,0,1),np.clip((1.9-t)/.45,0,1))*(.3+.7*np.minimum(t/1.35,1))
x=(.06*np.sin(2*np.pi*113*t)+.04*np.sin(2*np.pi*337*t)+noise*.5)*env
for at in (.18,.43,.81,1.16,1.34):
 u=np.maximum(0,t-at)
 x+=noise*(t>=at)*np.exp(-u/.035)*.22
x*=np.clip((1.9-t)/.08,0,1)
x*=.60/max(abs(x))
out=Path(__file__).resolve().parents[1]/'SFX'/'collapse_build.wav'
with wave.open(str(out),'wb') as w:
 w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate);w.writeframes((x*32767).astype('<i2').tobytes())
print(f'{out.name}: {len(t)/rate:.2f}s, peak {max(abs(x)):.2f}, clipped {sum(abs(x)>=1)}')

