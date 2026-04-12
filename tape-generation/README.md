# Tape Generation

This document is the main guide for the `tape-generation` app.

## What this module does

`tape-generation` renders and projects glyph masks into physical tape outputs using geometric projection between:

- display segment frames,
- slit frames,
- and reconstructed light-source positions.

Outputs are written to configured folders as PNG masks and composed tape images.

## Project layout

- `Program.cs` — end-to-end pipeline orchestration.
- `ConfigManager.cs` — JSON config models and loader.
- `TextSampler.cs` — font rendering + bitmap sampling.
- `ProjectionUtils.cs` — bitmap utility helpers.
- `ProjectionModels.cs` — projection/sample DTO models.
- `phys/` — geometry primitives and math (`Frame`, `Point3D`, `Vector3D`, `Plane`, `Ray`, `GeometryMath`).
- `config/` — runtime JSON configuration and font asset.
- `render/` and `tapes/` — generated output folders.

## Run

From repository root:

```bash
dotnet run --project ./tape-generation/tape-generation.csproj
```

> Note: runtime configuration is loaded from `tape-generation/config/*.json` via relative paths used in `Program.cs`.

## Configuration files

- `config/paths.json`
  - output locations for rendered masks and tape exports.
- `config/tape-config.json`
  - tape text, offsets, geometry dimensions, DPI, font sizes, debug toggle.
- `config/world-geometry.json`
  - slit/display physical setup and 3D orientation.

## Processing flow (high level)

1. Load config (`Config.LoadAll`).
2. Build slit frames and display frames (`BuildFrameList`).
3. Reconstruct per-slit light sources (`ComputeLightSources`).
4. Render/snapshot characters (`TextSampler.RenderAndSampleCharacters`).
5. Project deadzone pixels through slit geometry onto tape plane (`GeometryMath.GetProjectionPoint`).
6. Build per-character/per-slit boolean masks (`ProjectionUtils.BuildSourceBitmap`).
7. Compose full tape images (`GeneratePhysicalTapes`).
8. Emit verification grid (`GenerateVerificationGrid`).

## Documentation docks

Detailed references live in `docks/`:

- [`docks/classes.md`](./docks/classes.md) — class/struct responsibilities and key properties.
- [`docks/functions.md`](./docks/functions.md) — function-by-function behavior and inputs/outputs.
