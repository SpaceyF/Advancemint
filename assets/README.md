# assets

`icon.ico`, `icon.png` and `advancement.wav` are mine and ship with the repo.

these three aren't (they're Mojang's), so drop your own here with these EXACT names and
it builds + runs. same files go in `linux/assets/` for the linux build.

| file | what / where |
|---|---|
| `Monocraft.ttf` | the minecraft-style font. free (OFL): https://github.com/IdreesInc/Monocraft |
| `now_playing.png` | toast panel background. the 160×32 minecraft "toast" sprite (dark rounded panel) |
| `music_notes.png` | particle sheet. 16×128 = eight stacked 16×16 note frames, opaque-on-transparent |

notes:
- `now_playing.png` gets nine-sliced with a 4px inset, so keep the border in the outer
  ~4px and a flat center.
- `music_notes.png` frames get alpha-masked and tinted gold for the burst, so the shape
  just needs to be opaque on transparent. greyscale is fine.
- missing them won't crash anything: the panel falls back to a plain dark box, the font
  falls back to Consolas, and the particles fall back to plain squares. it just looks
  worse.
