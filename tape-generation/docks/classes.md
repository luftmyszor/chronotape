# Classes and Structs Dock

## Configuration (`ConfigManager.cs`)

- `PathsConfig`: output/font/render path settings.
- `TapeConfig`: tape text, dimensions, offsets, DPI, font sizes, debug flag.
- `WorldGeometryConfig`: slit/display geometry and world-space vectors/points.
- `Config` (static): loads and exposes `Paths`, `Tape`, `WorldGeometry` via `LoadAll(...)`.

## Projection models (`ProjectionModels.cs`)

- `ProjectionOptions`: optional command options model (font, output, text, size, sample step).
- `CharacterBitmapSample`: per-character sampled bitmap (`Character`, `BitmapWidth`, `BitmapHeight`, `Pixels`).
- `SampledPixel`: sampled source-pixel data (`X`, `Y`, source bitmap dimensions).
- `SlitProjectionResult`: projected points for one slit (`SlitIndex`, `LightSource`, `Points`).
- `CharacterSlitBitmap`: bitmap result for one character (`Character`, `Bitmap`).
- `ProjectedPoint`: pixel and world/local coordinate container used across mapping/projection steps.

## Text and bitmap helpers

- `TextSampler` (`TextSampler.cs`, static): renders glyphs with SkiaSharp and samples opaque pixels.
- `ProjectionUtils` (`ProjectionUtils.cs`): converts projected points into 2D boolean bitmaps.

## Geometry primitives (`phys/`)

- `Point3D` (`Point.cs`): 3D point container and point subtraction operator.
- `Vector3D` (`Vector.cs`): 3D vector math helpers (`Dot`, `Cross`, `MakeUpInPlane`).
- `Ray` (`Ray.cs`): line/ray definition (`Origin`, `Direction`).
- `Plane` (`Plane.cs`): plane definition (`Point`, `Normal`).
- `Frame` (`Frame.cs`): oriented rectangle in 3D with corners, center, and mapping methods.
- `GeometryMath` (`GeometryMath.cs`, static): ray-fit and projection/intersection math.
