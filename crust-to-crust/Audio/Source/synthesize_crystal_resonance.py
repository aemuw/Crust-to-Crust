"""Original dry glass resonance cue; reproducible locally, no paid generation service."""
from pathlib import Path
import numpy as np
import wave
rate=44100
t=np.arange(int(1.65*rate))/rate
x=np.zeros_like(t)
for i,f in enumerate((438,711,1139,1763,2489)):
    envelope=(1-np.exp(-t/0.018))*np.exp(-t/(1.15-i*.13))
    x+=envelope*np.sin(2*np.pi*f*t+0.6*np.sin(2*np.pi*(2+i*.23)*t))/(1+i*.8)
# A second restrained strike anticipates the split at 1.25 s.
u=np.maximum(0,t-1.10)
x+=.23*(t>1.10)*(1-np.exp(-u/.003))*np.exp(-u/.10)*np.sin(2*np.pi*2917*u)
x*=np.clip((1.65-t)/.14,0,1)
x*=.62/np.max(np.abs(x))
out=Path(__file__).resolve().parents[1]/'SFX'/'crystal_resonance.wav'
with wave.open(str(out),'wb') as w:
    w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate);w.writeframes((x*32767).astype('<i2').tobytes())
print(f'{out.name}: {len(x)/rate:.2f}s, peak {max(abs(x)):.2f}, clipped {sum(abs(x)>=1)}')
