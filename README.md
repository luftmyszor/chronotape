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
- Deadzone aperture uses slit width/height from world geometry and a tape-local vertical offset (`SlitCenterYOffsetPx`).
- Main glyphs are trimmed to opaque bounds before compositing, so final spacing is controlled by configured horizontal/vertical main padding.
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

Chronotape now uses two config files as source-of-truth:

1. `--tape-config` for tape-specific values (characters, DPI, paddings, output/font, etc.)
2. `--world-geometry` for shared real-world geometry in millimeters (slits, planes, spacing, display segment sizes)

Tape generation converts millimeters to pixels only for rasterization.

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

### Millimeter-based geometry config (`--tape-config`)

Millimeter values are converted with:

- `px = round(mm * dpi / 25.4)`
- rounding mode: `MidpointRounding.AwayFromZero`

`Dpi` is defined in config JSON.

Supported `--tape-config` geometry fields:

- `Dpi`
- `SegmentWidthMm`, `SegmentHeightMm`, `TopMarginMm`
- `MainHorizontalPaddingMm`, `MainVerticalPaddingMm`
- `SlitCenterYOffsetMm`

Shared physical geometry fields are defined in `world-geometry.json` and include:

- `SlitWidthMm`, `SlitHeightMm`
- `SlitCount`, `SlitSegmentCenterDistanceMm`, `TapeTopHeightFromGroundMm`
- `DisplayedSegmentWidthMm`, `DisplayedSegmentHeightMm`, `DisplayedSegmentCenterDistanceMm`
- tape plane / slit orientation vectors and display plane point/normal/up vectors

### Generate using config only (including `FontPath`)

`tape-config.json`:

```json
{
  "SegmentCharacters": "7391",
  "MainCharacters": "9137",
  "Offset": 1,
  "Dpi": 600,
  "SegmentWidthMm": 25.4,
  "SegmentHeightMm": 50.8,
  "TopMarginMm": 12.7,
  "MainHorizontalPaddingMm": 0.5,
  "MainVerticalPaddingMm": 0.5,
  "SlitCenterYOffsetMm": 21.844,
  "FontPath": "/absolute/path/to/font.ttf",
  "OutputPath": "./tape-font.png"
}
```

Run:

```bash
  dotnet run --project ./tape-gen/tape-gen.csproj -- \
  --generate-tape \
  --tape-config ./tape-config.json \
  --world-geometry ./tape-gen/world-geometry.json
```

### Generate using config + CLI overrides

```bash
  dotnet run --project ./tape-gen/tape-gen.csproj -- \
  --generate-tape \
  --tape-config ./tape-config.json \
  --world-geometry ./tape-gen/world-geometry.json \
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
  --world-geometry ./tape-gen/world-geometry.json \
  --out ./projection-debug
```

This writes intermediate artifacts such as:

- `./projection-debug/rendered/*.png` (sampled rendered glyph bitmaps)
- `./projection-debug/projected/slit-*/*.png` (projected glyph bitmaps per slit)

### Tape rectangle debug visuals

- `--debug-rects`: existing outline-only debug rectangles
- `--highlight-rects`: high-contrast overlay mode (semi-transparent fills + bold outlines) for segment and deadzone bounds

Both modes can be enabled together and work for multi-slit output.

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
