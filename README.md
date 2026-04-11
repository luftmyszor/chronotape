# chronotape

## Tape bitmap generation

`tape-gen` now includes reusable tape rendering/export API:

- `TapeBitmapGenerator.GenerateTapeBitmap(TapeSpec spec)`
- `TapeBitmapGenerator.GenerateTapeBitmap(TapeSpec spec, int slitIndex)`
- `TapeBitmapGenerator.ExportTape(TapeSpec spec)`

### `TapeSpec` behavior

- `SegmentCharacters` length defines segment count `N`.
- Main character rule: `MainCharacters` is required and must have exactly length `N`.
- Deadzone mapping per segment index `i`: `SegmentCharacters[(i + Offset) % N]`.
- Deadzone rectangle is configured per segment via `DeadzoneRectPx`.
- Main glyphs are centered by fixed 7-segment cell center (`'8'` reference cell), not per-glyph visual bounds.
- Deadzone glyphs are projected into fixed slit positions (not re-centered per glyph).
- If `SlitCount > 1`, `ExportTape` writes one file per slit using suffixes:
  - `...-slit-0.png`, `...-slit-1.png`, etc.

### Generate a tape from actual values

Generate tape output(s) from values you provide:

```bash
dotnet run --project ./tape-gen/tape-gen.csproj -- \
  --generate-tape \
  --segment-characters 7391 \
  --main-characters 9137 \
  --font /absolute/path/to/font.ttf \
  --offset 1 \
  --slit-count 2 \
  --tape-out ./tape.png
```

Input precedence:

1. CLI flags
2. Environment variables
3. `--tape-config <path-to-json>`
4. Built-in non-sample defaults (layout/output defaults only)

Required values (must be provided via CLI/env/config):

- `SegmentCharacters` (`--segment-characters` / `CHRONOTAPE_SEGMENT_CHARACTERS`)
- `MainCharacters` (`--main-characters` / `CHRONOTAPE_MAIN_CHARACTERS`)

Font mode:

- If `FontPath` (or `--font`) is provided, tape generation uses that font for both main glyphs and deadzone glyphs.
- Deadzone glyphs are generated through the projection pipeline and then composited into the deadzone region.
- If no font is provided, legacy generation remains the default.
- Font path source precedence follows the same rule (`CLI > env > config > defaults`) using:
  - `--font`
  - `CHRONOTAPE_FONT_PATH`
  - `FontPath` in `--tape-config` JSON

If required values are missing, tape generation fails with a clear error instead of falling back to sample values.

If a font path is provided but invalid, generation fails with `Font file does not exist: ...`.

### Generate using config only (including `FontPath`)

`tape-config.json`:

```json
{
  "SegmentCharacters": "7391",
  "MainCharacters": "9137",
  "Offset": 1,
  "SlitCount": 2,
  "FontPath": "/absolute/path/to/font.ttf",
  "OutputPath": "./tape-font.png"
}
```

Run:

```bash
dotnet run --project ./tape-gen/tape-gen.csproj -- \
  --generate-tape \
  --tape-config ./tape-config.json
```

### Generate using config + CLI overrides

```bash
dotnet run --project ./tape-gen/tape-gen.csproj -- \
  --generate-tape \
  --tape-config ./tape-config.json \
  --font /absolute/path/to/override-font.otf \
  --offset 3 \
  --tape-out ./tape-overridden.png
```

### Projection debug mode

Use debug mode only when tuning geometry/projection artifacts:

```bash
dotnet run --project ./tape-gen/tape-gen.csproj -- \
  --projection-debug \
  --font /absolute/path/to/font.ttf \
  --out ./projection-debug
```

This writes intermediate artifacts such as:

- `./projection-debug/rendered/*.png` (sampled rendered glyph bitmaps)
- `./projection-debug/projected/slit-*/*.png` (projected glyph bitmaps per slit)

### Sample CLI (documentation/testing)

Generate sample tape output(s):

```bash
dotnet run --project ./tape-gen/tape-gen.csproj -- --sample-tape --sample-out ./tape.png
```

Sample values used:

- `SegmentCharacters = "1234"`
- `MainCharacters = "1234"`
- `Offset = 2`
- `SlitCount = 4`
