"""Original core-cavity pressure rise, produced locally without external samples."""
from pathlib import Path
import numpy as np
import wave

rate=44100
t=np.arange(int(rate*2.4))/rate
rng=np.random.default_rng(1102026)
noise=rng.normal(0,1,len(t))
noise=np.convolve(noise,np.ones(130)/130,mode='same')
envelope=np.minimum(1,t/.25)*np.minimum(1,(2.4-t)/.7)
signal=(.16*np.sin(2*np.pi*74*t)+.09*np.sin(2*np.pi*111*t)+noise*.5)*envelope
for start in (.12,.55,1.05):
    u=np.maximum(0,t-start)
    signal+=(t>=start)*np.minimum(1,u/.003)*np.exp(-u/.12)*(.035*np.sin(2*np.pi*319*u)+.025*rng.normal(0,1,len(t)))
signal*=.6/np.max(np.abs(signal))
path=Path(__file__).resolve().parents[1]/'SFX'/'core_opening.wav'
with wave.open(str(path),'wb') as w:
    w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate)
    w.writeframes((signal*32767).astype('<i2').tobytes())
print(f'{path.name}: 2.40s, peak {np.max(np.abs(signal)):.2f}, clipped {np.sum(np.abs(signal)>=1)}')
