# Audio credits

## Music (`Music/`)

All four tracks were generated with **Google Gemini's "음악 만들기" (Create music) tool** on **2026-11-08**, using the user's Gemini Pro account (gemini.google.com in Chrome). They were downloaded as "오디오만" (audio only) MP3 files. Each prompt asked for instrumental music with no vocals that does not resemble any existing artist, song or game soundtrack.

Check Google's terms for AI-generated music before a commercial release.

| File | Length | Used for | Prompt |
|---|---|---|---|
| `field_ashen_ruins.mp3` | 2:10 | Field (all zones, Z0 잿빛 폐허 mood) | Instrumental only, no vocals, no lyrics. A loopable 2-minute ambient exploration theme for an indie video game: a lone hooded wanderer robot walking through grey ash-covered ruins of a dead, post-apocalyptic city. Lonely, quiet and melancholic. Sparse, slightly detuned old upright piano with soft felt notes, a few distant fingerpicked acoustic guitar notes, low wind texture, faint far-away mechanical hums and metallic creaks. Slow tempo around 60 BPM, lots of space and silence. The ending should flow seamlessly back into the beginning for looping. Original composition; do not imitate or resemble any existing artist, song, or video game soundtrack. |
| `boss_ash_warden.mp3` | 2:01 | Boss fight (재의 감시자). Phase 2 jumps to the second half | Instrumental only, no vocals, no lyrics. A loopable 2-minute boss battle theme for an indie action game: a small hooded robot fights the Ash Warden, a towering scrap-metal machine haunted by a vengeful human spirit, in a ruined grey city. Heavy, tense and driving. Deep taiko-like drums, industrial metallic percussion (anvil strikes, clanging scrap metal, hammered pipes), low distorted cello ostinato, dark brass stabs, grinding machine textures. Around 140 BPM. First half tense and menacing; second half a more intense, frantic variation for the boss's second phase, then loop back to the start seamlessly. Original composition; … |
| `purify_bloom.mp3` | 0:57 | Purification after the boss (plays once) | Instrumental only, no vocals, no lyrics. A short, warm and hopeful 1-minute piece for the moment after the boss is defeated in an indie game: a dead grey world is purified and life returns, grass and flowers grow back, birds and butterflies appear. Starts fragile with a lone soft piano and a gentle music-box motif, then blooms with warm strings, soft felt piano chords, light harp and a subtle choir-like synth pad (no words). Bright major key, slow and tender, ends on a calm resolved chord. Original composition; … |
| `rest_pod.mp3` | 1:29 | Resting at a charging pod (G), stops when you walk away | Instrumental only, no vocals, no lyrics. A loopable 1.5-minute calm resting theme for an indie game: a small robot recharges inside a glowing charging pod, a safe haven in a ruined world. Gentle, soothing and a little nostalgic. A soft music box melody over warm analog synth pads, faint electrical hum and quiet tape hiss, slow tempo around 70 BPM. The ending should flow seamlessly into the beginning for looping. Original composition; … |

("…" = same closing sentence as the first row.)

## Sound effects

There are no audio files for sound effects. Every effect is synthesized at startup by `Runtime/Audio/SfxSynth.cs` from noise, oscillators, envelopes and filters, so there is nothing to credit.
