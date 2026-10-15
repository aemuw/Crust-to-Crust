"""Original industrial drive loop and relay cue; deterministic local synthesis."""
from pathlib import Path
import numpy as np
import wave

rate = 44100
out = Path(__file__).resolve().parents[1] / 'SFX'
def save(name, signal):
    signal *= .60 / max(abs(signal))
    with wave.open(str(out/name), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(rate)
        w.writeframes((signal*32767).astype('<i2').tobytes())
    print(f'{name}: {len(signal)/rate:.2f}s, peak {max(abs(signal)):.2f}, clipped {sum(abs(signal)>=1)}')

t = np.arange(rate*2)/rate
drive = (.12*np.sin(2*np.pi*88*t)+.075*np.sin(2*np.pi*176*t)+.035*np.sin(2*np.pi*440*t))
drive *= .7+.3*np.sin(2*np.pi*6*t)**2
drive += .025*np.sin(2*np.pi*1320*t)*np.maximum(0,np.cos(2*np.pi*12*t))**12
save('drill_motor.wav', drive)

t = np.arange(int(rate*1.1))/rate
rng = np.random.default_rng(9102026)
noise = rng.normal(0,1,len(t))
signal = np.zeros_like(t)
for start in (0,.28,.64):
    u = np.maximum(0,t-start)
    env = (t>=start)*np.exp(-u/.055)*np.minimum(1,u/.003)
    signal += env*(.16*noise+.24*np.sin(2*np.pi*613*u)+.1*np.sin(2*np.pi*1097*u))
signal += .04*np.sin(2*np.pi*(220*t+100*t*t))*np.sin(np.pi*t/1.1)**2
signal *= np.clip((1.1-t)/.1,0,1)
save('drill_engage.wav', signal)
