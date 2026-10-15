"""Original mineral resonance and brittle fracture; no external samples or services."""
from pathlib import Path
import numpy as np
import wave

rate = 44100
rng = np.random.default_rng(10102026)
out = Path(__file__).resolve().parents[1] / 'SFX'

def save(name, sound):
    sound *= .60 / np.max(np.abs(sound))
    with wave.open(str(out / name), 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes((sound * 32767).astype('<i2').tobytes())
    print(f'{name}: {len(sound)/rate:.2f}s, peak {np.max(np.abs(sound)):.2f}, clipped {np.sum(np.abs(sound)>=1)}')

t = np.arange(rate * 2) / rate
sound = np.zeros_like(t)
for start in (0, .38, .78):
    u = np.maximum(0, t-start)
    envelope = (t>=start) * np.minimum(1, u/.008) * np.exp(-u/.36)
    for frequency, gain in ((523, .16), (831, .095), (1247, .055), (2089, .025)):
        sound += gain * np.sin(2*np.pi*frequency*u) * envelope
sound += .035 * np.sin(2*np.pi*92*t) * np.sin(np.pi*t/2)**2
sound *= np.minimum(1, (2-t)/.12)
save('crystal_chamber_resonance.wav', sound)

t = np.arange(int(rate*.95)) / rate
sound = rng.normal(0, 1, len(t)) * .14 * np.exp(-t/.045)
for frequency in (733, 1171, 1879, 2797):
    sound += .07 * np.sin(2*np.pi*frequency*t) * np.exp(-t/.22)
for start in (.08, .16, .29):
    u = np.maximum(0,t-start)
    sound += .065 * rng.normal(0,1,len(t)) * (t>=start) * np.exp(-u/.018)
sound *= np.minimum(1,t/.002) * np.minimum(1,(.95-t)/.1)
save('crystal_chamber_fracture.wav', sound)
