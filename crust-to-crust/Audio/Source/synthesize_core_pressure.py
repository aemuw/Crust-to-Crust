"""Original core-pressure warning and restrained gust, deterministic local synthesis."""
from pathlib import Path
import numpy as np
import wave
rate=44100
t=np.arange(int(2.45*rate))/rate
rng=np.random.default_rng(7102026)
f=np.fft.rfftfreq(len(t),1/rate)
spectrum=np.fft.rfft(rng.normal(0,1,len(t)))
wind=np.fft.irfft(spectrum/(1+(f/1600)**2)*f/(f+90),n=len(t))
wind/=np.max(np.abs(wind))
warning=np.minimum(np.clip(t/.14,0,1),np.clip((1.16-t)/.12,0,1))
build=(.16+.48*np.clip(t/1.1,0,1))*warning
u=np.clip((t-1.1)/.9,0,1)
pulse=np.sin(np.pi*u)**1.1
x=(.08*np.sin(2*np.pi*(142*t+15*t*t))+.035*np.sin(2*np.pi*423*t))*build
x+=wind*(.10*build+.65*pulse)
x+=.06*np.sin(2*np.pi*96*t)*pulse
x*=np.clip((2.45-t)/.15,0,1)
x*=.60/np.max(np.abs(x))
out=Path(__file__).resolve().parents[1]/'SFX'/'core_pressure.wav'
with wave.open(str(out),'wb') as w:
    w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate);w.writeframes((x*32767).astype('<i2').tobytes())
print(f'{out.name}: {len(t)/rate:.2f}s, peak {max(abs(x)):.2f}, clipped {sum(abs(x)>=1)}')
