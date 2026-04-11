# chronotape

## Tape bitmap generation

`tape-gen` now includes reusable tape rendering/export API:

- `TapeBitmapGenerator.GenerateTapeBitmap(TapeSpec spec)`
- `TapeBitmapGenerator.GenerateTapeBitmap(TapeSpec spec, int slitIndex)`
- `TapeBitmapGenerator.ExportTape(TapeSpec spec)`

### `TapeSpec` behavior

- `SegmentCharacters` length defines segment count `N`.
- **Main character rule (Option A):** `MainCharacters` is required and must have exactly length `N`.
- Deadzone mapping per segment index `i`: `SegmentCharacters[(i + Offset) % N]`.
- Deadzone rectangle is configured per segment via `DeadzoneRectPx`.
- Main glyphs are centered by fixed 7-segment cell center (`'8'` reference cell), not per-glyph visual bounds.
- Deadzone glyphs are projected into fixed slit positions (not re-centered per glyph).
- If `SlitCount > 1`, `ExportTape` writes one file per slit using suffixes:
  - `...-slit-0.png`, `...-slit-1.png`, etc.

### Sample CLI

Generate sample tape output(s):

```bash
dotnet run --project ./tape-gen/tape-gen.csproj -- --sample-tape --sample-out ./tape.png
```

Sample values used:

- `SegmentCharacters = "1234"`
- `MainCharacters = "1234"`
- `Offset = 2`
- `SlitCount = 4`
