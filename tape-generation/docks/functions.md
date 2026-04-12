# Functions Dock

## `Program.cs`

### Setup and orchestration

- `Config.LoadAll(pathsFilePath, tapeFilePath, geometryFilePath)`
  - Loads all runtime JSON configuration before processing.
- `BuildFrameList(origin, direction, normal, up, centerDistance, frameWidth, frameHeightMm, amount)`
  - Builds evenly spaced oriented `Frame` instances (slits or displayed segments).
- `ComputeLightSources(slits, displayedSegments)`
  - For each slit/display pair, computes best-fit light source by intersecting four corner rays.

### Projection and generation pipeline

- `SaveBoolBitmap(bitmap, path)`
  - Writes `bool[][]` masks to PNG (white=true, black=false).
- `GeneratePhysicalTapes(mainMasks, deadzoneMasks, slitCount, outputPath, debug)`
  - Composes final tape PNGs per slit from main + projected deadzone masks.
- `DrawMaskToCanvas(canvas, mask, offsetX, offsetY)`
  - Blits mask pixels into a target canvas with transparent background handling.
- `GenerateVerificationGrid(allGeneratedTapeMasks, deadzoneCharacterBitmaps, slits, displayedSegments, lightSources, outputPath)`
  - Reverse-projects generated masks into a master display frame and writes debug/verification grid.
- `MmToPx(mm)`
  - Converts millimeters to pixels using configured DPI (`round(mm * dpi / 25.4)`).

## `ConfigManager.cs`

- `Config.LoadAll(pathsFilePath, tapeFilePath, geometryFilePath)`
  - Loads all config models and stores them in static properties.
- `Config.LoadConfig<T>(filePath)` (private)
  - Reads JSON file, deserializes into target model, and throws descriptive errors on failure.

## `TextSampler.cs`

- `TextSampler.RenderAndSampleCharacters(fontPath, text, textSize, sampleStep)`
  - Renders each character with SkiaSharp and returns sampled opaque pixels.
- `TextSampler.RenderAndSampleCharacter(character, paint, sampleStep)` (private)
  - Creates one glyph bitmap, draws text baseline, samples non-transparent pixels at step size.

## `ProjectionUtils.cs`

- `ProjectionUtils.BuildSourceBitmap(width, height, points)`
  - Builds a `bool[][]` bitmap from projected pixel coordinates with bounds checks.
- `ProjectionUtils.BuildEmptyBitmap(width, height)` (private)
  - Creates empty bitmap storage.

## `phys/Frame.cs`

- `Frame(...)` constructor
  - Builds oriented rectangle corners from center, normal/up basis, width/height.
- `MapPixelTo3D(pixelX, pixelY, width, height)`
  - Maps bitmap pixel coordinate into 3D point on frame surface.
- `Map3DToPixel(point3D, targetWidth, targetHeight)`
  - Maps 3D frame point back to pixel coordinate.
- `Map3DToUV(point3D)`
  - Maps 3D frame point to normalized frame UV coordinates.

## `phys/GeometryMath.cs`

- `GeometryMath.GetClosestPointToRays(rays, out intersection)`
  - Computes least-squares 3D intersection point for multiple rays.
- `GeometryMath.GetProjectionPoint(startPoint, pointOnPlaneA, targetPlane, out finalPoint)`
  - Projects a ray from source through an intermediate point and intersects with target plane.

## `phys/Vector.cs`

- `Vector3D.Cross(a, b)` / `Vector3D.Dot(a, b)`
  - Core vector operations.
- `Vector3D.MakeUpInPlane(normal, hint)`
  - Produces a stable in-plane up vector from normal + hint.
