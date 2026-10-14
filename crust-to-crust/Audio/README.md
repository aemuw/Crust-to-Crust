Audio layout:

- `Music/`: menu and gameplay scores.
- `Ambience/`: the existing cave noise and falling wind.
- `SFX/`: playback effects, including distinct cooling and fissure discharge sounds.
- `Source/`: original imported recordings, retained for editing; not played directly.

The cryogenic WAV effects are original locally synthesized stereo PCM audio,
with filtered air, a short pressure envelope and sparse ice crackles. They contain
no liquid splash or diving sound. The menu ambience still plays under its score.

`Ambience/core_rumble.wav` is a locally synthesized periodic low-frequency bed
with upper harmonics for small speakers. Its gain follows proximity to the core.
`SFX/core_crossing.wav` is a separate short tectonic swell, played once per crossing.
Both follow the effects volume and pause with gameplay; the music remains unchanged.
