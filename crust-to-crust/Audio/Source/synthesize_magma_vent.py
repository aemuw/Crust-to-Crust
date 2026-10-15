"""Original dry pressure warning and hot mineral-gas release; local deterministic synthesis."""
from pathlib import Path
import numpy as np
import wave
rate=44100
t=np.arange(int(rate*2.5))/rate
rng=np.random.default_rng(6102026)
f=np.fft.rfftfreq(len(t),1/rate)
raw=np.fft.rfft(rng.normal(0,1,len(t)))
noise=np.fft.irfft(raw*(f/(f+250))/(1+(f/3400)**2),n=len(t))
noise/=np.max(np.abs(noise))
warning=np.clip(t/.12,0,1)*np.clip((1.16-t)/.08,0,1)*(.055+.10*np.clip(t/1.1,0,1))
u=np.maximum(0,t-1.1)
release=(t>=1.1)*np.clip(u/.055,0,1)*np.clip((1.25-u)/.22,0,1)
x=noise*(warning+.8*release)
x+=.025*np.sin(2*np.pi*(161*t+8*t*t))*warning
x+=.05*np.sin(2*np.pi*83*t)*release*np.exp(-u*3)
x*=np.clip((2.5-t)/.1,0,1)
x*=.62/np.max(np.abs(x))
out=Path(__file__).resolve().parents[1]/'SFX'/'magma_vent.wav'
with wave.open(str(out),'wb') as w:
    w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate);w.writeframes((x*32767).astype('<i2').tobytes())
print(f'{out.name}: {len(t)/rate:.2f}s, peak {max(abs(x)):.2f}, clipped {sum(abs(x)>=1)}')
