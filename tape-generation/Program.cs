using SkiaSharp;
using Phys;
using Configuration;
using System.IO;
using System.Linq;
using System.CodeDom.Compiler;

Config.LoadAll(
            pathsFilePath: "config/paths.json",
            tapeFilePath: "config/tape-config.json",
            geometryFilePath: "config/world-geometry.json"
        );



List<Frame> slits = BuildFrameList(
    Config.WorldGeometry.TapeOriginMm,
    Config.WorldGeometry.SlitDirection,
    Config.WorldGeometry.SlitNormal,
    Config.WorldGeometry.SlitUpDirection,
    Config.WorldGeometry.SlitSegmentCenterDistanceMm,
    Config.WorldGeometry.SlitWidthMm,
    Config.WorldGeometry.SlitHeightMm,
    Config.WorldGeometry.SlitCount);

List<Frame> displayedSegments = BuildFrameList(
    Config.WorldGeometry.DisplayPlanePointMm,
    Config.WorldGeometry.SlitDirection,
    Config.WorldGeometry.DisplayPlaneNormal,
    Config.WorldGeometry.DisplayPlaneUpDirection,
    Config.WorldGeometry.DisplayedSegmentCenterDistanceMm,
    Config.WorldGeometry.DisplayedSegmentWidthMm,
    Config.WorldGeometry.DisplayedSegmentHeightMm,
    Config.WorldGeometry.SlitCount);
Point3D?[] lightSources = ComputeLightSources(slits, displayedSegments);

Console.WriteLine("\n\n");
for (int i = 0; i < lightSources.Length; i++)
{
    Console.WriteLine($"==== Set {i} ====");
    Console.WriteLine($"Display Frame Center: {displayedSegments[i].Center}");
    Console.WriteLine($"Slit Frame Center: {slits[i].Center}");
    Console.WriteLine($"Light Source: {lightSources[i]}");
}


Directory.CreateDirectory(Config.Paths.Render);
string renderedDir = Path.Combine(Config.Paths.Render, "rendered");
string projectedDir = Path.Combine(Config.Paths.Render, "projected");
Directory.CreateDirectory(renderedDir);
Directory.CreateDirectory(projectedDir);

List<CharacterBitmapSample> deadzoneBaseCharacterBitmaps = TextSampler.RenderAndSampleCharacters(Config.Paths.DeadzoneFont, Config.Tape.DeadzoneCharacters, MmToPx(Config.Tape.DeadGlyphFontSizeMm), 1);
List<CharacterBitmapSample> mainCharacterBitmaps = TextSampler.RenderAndSampleCharacters(Config.Paths.DeadzoneFont, Config.Tape.DeadzoneCharacters, MmToPx(Config.Tape.MainGlyphFontSizeMm), 1);

// Flip vertical coordinates for bottom-viewing immediately after sampling
foreach (var sample in deadzoneBaseCharacterBitmaps)
{
    foreach (var pixel in sample.Pixels)
    {
        pixel.Y = (sample.BitmapHeight - 1) - pixel.Y;
    }
}
// Store the generated masks: Rows = Characters, Columns = Slits
List<List<bool[][]>> allGeneratedTapeMasks = new List<List<bool[][]>>();
List<bool[][]> allMainMasks = new List<bool[][]>();

for (int charIndex = 0; charIndex < mainCharacterBitmaps.Count; charIndex++)
{
    CharacterBitmapSample mainSample = mainCharacterBitmaps[charIndex];

    // Save main character bitmap (these stay in the root 'rendered' folder)
    bool[][] renderedMainBitmap = ProjectionUtils.BuildSourceBitmap(
        mainSample.BitmapWidth,
        mainSample.BitmapHeight,
        mainSample.Pixels.Select(pixel => new ProjectedPoint { PixelX = pixel.X, PixelY = pixel.Y }).ToList()
    );
    SaveBoolBitmap(renderedMainBitmap, Path.Combine(renderedDir, $"main-{charIndex:D2}-{mainSample.Character}.png"));

    CharacterBitmapSample deadSample = deadzoneBaseCharacterBitmaps[charIndex];

    List<bool[][]> characterSlitMasks = new List<bool[][]>();


    for (int s = 0; s < slits.Count; s++)
    {
        Frame displayFrame = displayedSegments[s];
        Frame tapeFrame = slits[s];
        Point3D? lightSourcePos = lightSources[s];

        if (lightSourcePos == null) continue;

        string slitDir = Path.Combine(projectedDir, $"slit-{s:D2}");
        Directory.CreateDirectory(slitDir);

        Plane tapePlane = new Plane { Point = tapeFrame.Center, Normal = Config.WorldGeometry.SlitNormal };
        List<ProjectedPoint> slitSpecificDeadzonePixels = new List<ProjectedPoint>();

        // We want the mask to be the EXACT size of the physical tape segment
        int segmentWidthPx = MmToPx(Config.Tape.SegmentWidthMm);
        int segmentHeightPx = MmToPx(Config.Tape.SegmentHeightMm);

        // Calculate the physical mm position of the Slit's Top-Left corner on the Tape Segment
        double slitTopLeftX_mm = (Config.Tape.SegmentWidthMm / 2.0) - (Config.WorldGeometry.SlitWidthMm / 2.0);
        double slitTopLeftY_mm = Config.Tape.SlitCenterYOffsetMm - (Config.WorldGeometry.SlitHeightMm / 2.0);

        foreach (var pixel in deadSample.Pixels)
        {
            Point3D displayPoint3D = displayFrame.MapPixelTo3D(pixel.X, pixel.Y, deadSample.BitmapWidth, deadSample.BitmapHeight);

            if (GeometryMath.GetProjectionPoint(lightSourcePos.Value, displayPoint3D, tapePlane, out Point3D tapePoint3D))
            {
                // 1. Get raw float position on the Slit Frame
                var uv = tapeFrame.Map3DToUV(tapePoint3D);

                // 2. Convert to mm relative to the Slit's Top-Left corner
                double hitX_fromSlitLeft_mm = uv.U * Config.WorldGeometry.SlitWidthMm;
                double hitY_fromSlitTop_mm = uv.V * Config.WorldGeometry.SlitHeightMm;

                // 3. Translate to Absolute MM on the full Tape Segment
                double absoluteX_mm = slitTopLeftX_mm + hitX_fromSlitLeft_mm;
                double absoluteY_mm = slitTopLeftY_mm + hitY_fromSlitTop_mm;

                // 4. Convert Absolute MM into final pixel coordinates
                int pxX = MmToPx(absoluteX_mm);
                int pxY = MmToPx(absoluteY_mm);

                // Add it if it hits the tape backing!
                if (pxX >= 0 && pxX < segmentWidthPx && pxY >= 0 && pxY < segmentHeightPx)
                {
                    slitSpecificDeadzonePixels.Add(new ProjectedPoint { PixelX = pxX, PixelY = pxY });
                }
            }
        }

        // Build the mask using the FULL segment dimensions
        bool[][] renderedDeadProjectionBaseBitmap = ProjectionUtils.BuildSourceBitmap(
            segmentWidthPx,
            segmentHeightPx,
            slitSpecificDeadzonePixels
        );

        string filename = $"glyph-{charIndex:D2}-{deadSample.Character}.png";
        SaveBoolBitmap(renderedDeadProjectionBaseBitmap, Path.Combine(slitDir, filename));

        characterSlitMasks.Add(renderedDeadProjectionBaseBitmap);

    }
    allMainMasks.Add(renderedMainBitmap);
    allGeneratedTapeMasks.Add(characterSlitMasks);
}

GeneratePhysicalTapes(allMainMasks, allGeneratedTapeMasks, slits.Count, Config.Paths.TapeOutput,
debug: Config.Tape.Debug);

GenerateVerificationGrid(allGeneratedTapeMasks, deadzoneBaseCharacterBitmaps, slits, displayedSegments, lightSources, Path.Combine(projectedDir, "verification-grid.png"));

// ============ HELPERS ============
Point3D?[] ComputeLightSources(List<Frame> slits, List<Frame> displayedSegments)
{
    var sources = new Point3D?[slits.Count];

    for (int i = 0; i < slits.Count; i++)
    {
        Frame display = displayedSegments[i];
        Frame slit = slits[i];

        var rays = new List<Ray>
            {
                new Ray(display.TopRight,    new Vector3D(display.TopRight,    slit.TopRight)),
                new Ray(display.TopLeft,     new Vector3D(display.TopLeft,     slit.TopLeft)),
                new Ray(display.BottomRight, new Vector3D(display.BottomRight, slit.BottomRight)),
                new Ray(display.BottomLeft,  new Vector3D(display.BottomLeft,  slit.BottomLeft))
            };

        if (!GeometryMath.GetClosestPointToRays(rays, out Point3D lightSource))
        {
            Console.WriteLine("No closest point found");
            continue;
        }

        sources[i] = lightSource;
    }

    return sources;
}

List<Frame> BuildFrameList(Point3D origin, Vector3D direction, Vector3D normal, Vector3D up, double centerDistance, double frameWidth, double frameHeightMm, int amount = 1)
{
    var result = new List<Frame>();
    double middleIndex = (amount - 1) / 2.0;

    for (int i = 0; i < amount; i++)
    {
        double offset = (i - middleIndex) * centerDistance;
        Point3D center = new Point3D(
            origin.X + (direction.X * offset),
            origin.Y + (direction.Y * offset),
            origin.Z + (direction.Z * offset)
        );
        result.Add(new Frame(center, normal, up, frameWidth, frameHeightMm));
    }

    return result;
}

void SaveBoolBitmap(bool[][] bitmap, string path)
{
    int height = bitmap.Length;
    int width = height == 0 ? 0 : bitmap[0].Length;
    using var image = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            image.SetPixel(x, y, bitmap[y][x] ? SKColors.White : SKColors.Black);
        }
    }

    using SKImage skImage = SKImage.FromBitmap(image);
    using SKData data = skImage.Encode(SKEncodedImageFormat.Png, 100);
    using FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
    data.SaveTo(stream);
}



void GenerateVerificationGrid(
    List<List<bool[][]>> allGeneratedTapeMasks,
    List<CharacterBitmapSample> deadzoneCharacterBitmaps,
    List<Frame> slits,
    List<Frame> displayedSegments,
    Point3D?[] lightSources,
    string outputPath)
{
    if (allGeneratedTapeMasks.Count == 0 || slits.Count == 0) return;

    // Helper functions for scaling
    int MmToPx(double mm) => (int)Math.Round(mm * Config.Tape.Dpi / 25.4);
    double PxToMm(int px) => (px * 25.4) / Config.Tape.Dpi;

    // --- NEW: Calculate the GLOBAL Master Display Frame ---
    // This represents the entire physical surface where all segments overlap.
    double totalDisplayWidthMm = (slits.Count - 1) * Config.WorldGeometry.DisplayedSegmentCenterDistanceMm + Config.WorldGeometry.DisplayedSegmentWidthMm;
    double totalDisplayHeightMm = Config.WorldGeometry.DisplayedSegmentHeightMm;

    Frame masterDisplayFrame = new Frame(
        Config.WorldGeometry.DisplayPlanePointMm,
        Config.WorldGeometry.DisplayPlaneNormal,
        Config.WorldGeometry.DisplayPlaneUpDirection,
        totalDisplayWidthMm,
        totalDisplayHeightMm
    );

    int masterDisplayPxW = MmToPx(totalDisplayWidthMm);
    int masterDisplayPxH = MmToPx(totalDisplayHeightMm);

    int cols = slits.Count + 1;
    int rows = allGeneratedTapeMasks.Count;

    using SKBitmap gridBitmap = new SKBitmap(cols * masterDisplayPxW, rows * masterDisplayPxH, SKColorType.Bgra8888, SKAlphaType.Premul);
    using SKCanvas canvas = new SKCanvas(gridBitmap);
    canvas.Clear(SKColors.Black);

    using SKPaint pixelPaint = new SKPaint { Color = SKColors.White, IsAntialias = false };
    using SKPaint bboxPaint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 2 };

    SKColor[] boxColors = new SKColor[] { SKColors.Red, SKColors.Green, SKColors.Blue, SKColors.Yellow, SKColors.Cyan, SKColors.Magenta };

    // Physical offsets on the tape segment
    double slitTopLeftX_mm = (Config.Tape.SegmentWidthMm / 2.0) - (Config.WorldGeometry.SlitWidthMm / 2.0);
    double slitTopLeftY_mm = Config.Tape.SlitCenterYOffsetMm - (Config.WorldGeometry.SlitHeightMm / 2.0);

    Plane displayPlane = new Plane { Point = masterDisplayFrame.Center, Normal = Config.WorldGeometry.DisplayPlaneNormal };

    for (int c = 0; c < rows; c++)
    {
        List<bool[][]> tapeMasksForChar = allGeneratedTapeMasks[c];
        List<ProjectedPoint> combinedPixels = new List<ProjectedPoint>();
        List<SKRectI> combinedBBoxes = new List<SKRectI>();

        for (int s = 0; s < slits.Count; s++)
        {
            Frame tapeFrame = slits[s];
            Point3D? lightSourcePos = lightSources[s];
            bool[][] tapeMask = tapeMasksForChar[s];

            if (lightSourcePos == null || tapeMask.Length == 0) continue;

            int tapeH = tapeMask.Length;
            int tapeW = tapeMask[0].Length;

            List<ProjectedPoint> reverseProjectedPixels = new List<ProjectedPoint>();
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;

            for (int tapeY = 0; tapeY < tapeH; tapeY++)
            {
                for (int tapeX = 0; tapeX < tapeW; tapeX++)
                {
                    if (tapeMask[tapeY][tapeX])
                    {
                        // 1. Convert Tape Segment Pixel back to Physical Tape MM
                        double absoluteX_mm = PxToMm(tapeX);
                        double absoluteY_mm = PxToMm(tapeY);

                        // 2. Find position relative to the physical Slit top-left
                        double hitX_fromSlitLeft_mm = absoluteX_mm - slitTopLeftX_mm;
                        double hitY_fromSlitTop_mm = absoluteY_mm - slitTopLeftY_mm;

                        // 3. Convert to UV (percentage across the physical slit)
                        double u = hitX_fromSlitLeft_mm / Config.WorldGeometry.SlitWidthMm;
                        double v = hitY_fromSlitTop_mm / Config.WorldGeometry.SlitHeightMm;

                        // 4. Map UV to the 3D Point on the specific Slit Frame
                        Vector3D topEdge = new Vector3D(tapeFrame.TopRight.X - tapeFrame.TopLeft.X, tapeFrame.TopRight.Y - tapeFrame.TopLeft.Y, tapeFrame.TopRight.Z - tapeFrame.TopLeft.Z);
                        Vector3D bottomEdge = new Vector3D(tapeFrame.BottomRight.X - tapeFrame.BottomLeft.X, tapeFrame.BottomRight.Y - tapeFrame.BottomLeft.Y, tapeFrame.BottomRight.Z - tapeFrame.BottomLeft.Z);

                        Point3D topPoint = new Point3D(tapeFrame.TopLeft.X + topEdge.X * u, tapeFrame.TopLeft.Y + topEdge.Y * u, tapeFrame.TopLeft.Z + topEdge.Z * u);
                        Point3D bottomPoint = new Point3D(tapeFrame.BottomLeft.X + bottomEdge.X * u, tapeFrame.BottomLeft.Y + bottomEdge.Y * u, tapeFrame.BottomLeft.Z + bottomEdge.Z * u);

                        Vector3D verticalEdge = new Vector3D(bottomPoint.X - topPoint.X, bottomPoint.Y - topPoint.Y, bottomPoint.Z - topPoint.Z);
                        Point3D tapePoint3D = new Point3D(topPoint.X + verticalEdge.X * v, topPoint.Y + verticalEdge.Y * v, topPoint.Z + verticalEdge.Z * v);

                        // 5. Cast ray from Light Source, THROUGH this slit point, to the Display Plane
                        if (GeometryMath.GetProjectionPoint(lightSourcePos.Value, tapePoint3D, displayPlane, out Point3D revDisplayPoint3D))
                        {
                            // 6. Map the intersection onto the GLOBAL Master Display Frame instead of the local segment!
                            ProjectedPoint revDisplayPixel = masterDisplayFrame.Map3DToPixel(revDisplayPoint3D, masterDisplayPxW, masterDisplayPxH);

                            // Optional bounds check to ensure the ray landed on the glass at all
                            if (revDisplayPixel.PixelX >= 0 && revDisplayPixel.PixelX < masterDisplayPxW &&
                                revDisplayPixel.PixelY >= 0 && revDisplayPixel.PixelY < masterDisplayPxH)
                            {
                                reverseProjectedPixels.Add(revDisplayPixel);
                                combinedPixels.Add(revDisplayPixel);

                                if (revDisplayPixel.PixelX < minX) minX = revDisplayPixel.PixelX;
                                if (revDisplayPixel.PixelX > maxX) maxX = revDisplayPixel.PixelX;
                                if (revDisplayPixel.PixelY < minY) minY = revDisplayPixel.PixelY;
                                if (revDisplayPixel.PixelY > maxY) maxY = revDisplayPixel.PixelY;
                            }
                        }
                    }
                }
            }

            // --- DRAW TO MASTER GRID CELL ---
            int cellOffsetX = s * masterDisplayPxW;
            int cellOffsetY = c * masterDisplayPxH;

            foreach (var rp in reverseProjectedPixels) canvas.DrawPoint(cellOffsetX + rp.PixelX, cellOffsetY + rp.PixelY, pixelPaint);

            if (minX <= maxX && minY <= maxY)
            {
                SKRectI bbox = new SKRectI(cellOffsetX + minX, cellOffsetY + minY, cellOffsetX + maxX, cellOffsetY + maxY);
                bboxPaint.Color = boxColors[s % boxColors.Length];
                canvas.DrawRect(bbox, bboxPaint);
                combinedBBoxes.Add(new SKRectI(minX, minY, maxX, maxY));
            }
        }

        // --- DRAW COMBINED VIEW ---
        int combinedOffsetX = slits.Count * masterDisplayPxW;
        int combinedOffsetY = c * masterDisplayPxH;

        foreach (var cp in combinedPixels) canvas.DrawPoint(combinedOffsetX + cp.PixelX, combinedOffsetY + cp.PixelY, pixelPaint);

        for (int s = 0; s < combinedBBoxes.Count; s++)
        {
            var box = combinedBBoxes[s];
            bboxPaint.Color = boxColors[s % boxColors.Length];
            canvas.DrawRect(new SKRectI(combinedOffsetX + box.Left, combinedOffsetY + box.Top, combinedOffsetX + box.Right, combinedOffsetY + box.Bottom), bboxPaint);
        }
    }

    using SKImage skImage = SKImage.FromBitmap(gridBitmap);
    using SKData data = skImage.Encode(SKEncodedImageFormat.Png, 100);
    using FileStream stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
    data.SaveTo(stream);

    Console.WriteLine($"\nTrue Verification grid generated at: {outputPath}");
}


void GeneratePhysicalTapes(
    List<bool[][]> mainMasks,
    List<List<bool[][]>> deadzoneMasks,
    int slitCount,
    string outputPath,
    bool debug = false)
{
    // --- DIAGNOSTIC LOGGING ---
    Console.WriteLine($"\n--- Generating Tapes (Debug: {debug}) ---");
    Console.WriteLine($"Main Masks Count: {mainMasks.Count}");
    Console.WriteLine($"Deadzone Characters Count: {deadzoneMasks.Count}");
    for (int i = 0; i < deadzoneMasks.Count; i++)
    {
        Console.WriteLine($"  Character {i} has {deadzoneMasks[i].Count} slit masks.");
    }
    // --------------------------

    int tapeWidthPx = MmToPx(Config.Tape.SegmentWidthMm);
    int segmentHeightPx = MmToPx(Config.Tape.SegmentHeightMm);
    int topMarginPx = MmToPx(Config.Tape.TopMarginMm);

    // Use the vertical padding as the distance BETWEEN segments
    int paddingBetweenSegmentsPx = MmToPx(Config.Tape.MainVerticalPaddingMm);

    // Add the padding spaces to the total tape height
    int totalTapeHeightPx = topMarginPx + (mainMasks.Count * segmentHeightPx) + (Math.Max(0, mainMasks.Count - 1) * paddingBetweenSegmentsPx);

    using SKPaint debugSegmentPaint = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Stroke, StrokeWidth = 3 };
    using SKPaint debugMainPaint = new SKPaint { Color = SKColors.Green, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
    using SKPaint debugDeadPaint = new SKPaint { Color = SKColors.Blue, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };

    Directory.CreateDirectory(outputPath);

    for (int s = 0; s < slitCount; s++)
    {
        using SKBitmap tapeBitmap = new SKBitmap(tapeWidthPx, totalTapeHeightPx, SKColorType.Bgra8888, SKAlphaType.Premul);
        using SKCanvas canvas = new SKCanvas(tapeBitmap);
        canvas.Clear(SKColors.Black);

        for (int c = 0; c < mainMasks.Count; c++)
        {
            // Advance the segment start Y by both the segment height AND the padding
            int segmentYStart = topMarginPx + (c * (segmentHeightPx + paddingBetweenSegmentsPx));

            if (debug)
            {
                canvas.DrawRect(0, segmentYStart, tapeWidthPx, segmentHeightPx, debugSegmentPaint);
            }

            // --- 1. SAFELY PLACE MAIN GLYPH ---
            bool[][] mainMask = c < mainMasks.Count ? mainMasks[c] : new bool[0][];
            int mainH = mainMask.Length;
            int mainW = mainH > 0 ? mainMask[0].Length : 0;

            // Center horizontally exactly in the middle of the tape
            int mainX = (tapeWidthPx - mainW) / 2;

            // Anchor the main glyph to the segment start to preserve relative position
            int mainY = segmentYStart;

            DrawMaskToCanvas(canvas, mainMask, mainX, mainY);

            if (debug) canvas.DrawRect(mainX, mainY, mainW, mainH, debugMainPaint);

            // --- 2. SAFELY PLACE DEADZONE GLYPH ---
            bool[][] deadMask = new bool[0][];
            if (c < deadzoneMasks.Count && s < deadzoneMasks[c].Count) deadMask = deadzoneMasks[(c + 1) % mainMasks.Count][s];

            // Draw it directly at the segment's starting Y.
            DrawMaskToCanvas(canvas, deadMask, 0, segmentYStart);

            if (debug)
            {
                // Draw a horizontal line showing exactly where the physical slit center is
                int slitCenterAbsoluteY = segmentYStart + MmToPx(Config.Tape.SlitCenterYOffsetMm);
                canvas.DrawLine(0, slitCenterAbsoluteY, tapeWidthPx, slitCenterAbsoluteY, debugDeadPaint);
            }
        }

        string debugSuffix = debug ? "-debug" : "";
        string path = Path.Combine(outputPath, $"physical-tape-slit{s:D2}{debugSuffix}.png");

        using SKImage skImage = SKImage.FromBitmap(tapeBitmap);
        using SKData data = skImage.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }

    Console.WriteLine($"Generated physical tapes (Debug: {debug}) at: {outputPath}");
}
// Quick helper to draw your bool arrays directly onto the SKCanvas
void DrawMaskToCanvas(SKCanvas canvas, bool[][] mask, int offsetX, int offsetY)
{
    int h = mask.Length;
    int w = h > 0 ? mask[0].Length : 0;

    // Safety check: if the mask is empty, do nothing
    if (w == 0 || h == 0) return;

    using SKBitmap tempBmp = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
    for (int y = 0; y < h; y++)
    {
        for (int x = 0; x < w; x++)
        {
            // Only draw the white pixels, leave the rest transparent so it overlays cleanly
            tempBmp.SetPixel(x, y, mask[y][x] ? SKColors.White : SKColors.Transparent);
        }
    }

    canvas.DrawBitmap(tempBmp, offsetX, offsetY);
}

int MmToPx(double mm) => (int)Math.Round(mm * Config.Tape.Dpi / 25.4);