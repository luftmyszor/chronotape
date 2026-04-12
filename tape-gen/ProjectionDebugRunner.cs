using Phys;
using SkiaSharp;

internal static class ProjectionDebugRunner
{
    private const int CellPaddingPx = 12;
    private const int RowSeparatorThicknessPx = 2;

    public static void Run(ProjectionOptions options)
    {
        Directory.CreateDirectory(options.OutPath);
        string renderedDir = Path.Combine(options.OutPath, "rendered");
        string projectedDir = Path.Combine(options.OutPath, "projected");
        Directory.CreateDirectory(renderedDir);
        Directory.CreateDirectory(projectedDir);

        List<CharacterBitmapSample> characterBitmaps = TextSampler.RenderAndSampleCharacters(options.FontPath, options.Text, options.TextSize, options.SampleStep);
        for (int charIndex = 0; charIndex < characterBitmaps.Count; charIndex++)
        {
            CharacterBitmapSample sample = characterBitmaps[charIndex];
            bool[][] renderedBitmap = ProjectionPipeline.BuildSourceBitmap(sample.BitmapWidth, sample.BitmapHeight, sample.Pixels.Select(pixel => new ProjectedPoint { PixelX = pixel.X, PixelY = pixel.Y }).ToList());
            SaveBoolBitmap(renderedBitmap, Path.Combine(renderedDir, $"{charIndex:D2}-{Sanitize(sample.Character)}.png"));
        }

        WorldGeometryConfig geometry = options.WorldGeometry;
        Point3D chronotapeFrameOrigin = new Point3D(geometry.TapeOriginMm.XMm, geometry.TapeOriginMm.YMm, geometry.TapeOriginMm.ZMm);
        Vector3D slitFramesDirection = NormalizeVector(new Vector3D(geometry.SlitDirection.X, geometry.SlitDirection.Y, geometry.SlitDirection.Z));
        Vector3D slitFrameNormal = NormalizeVector(new Vector3D(geometry.SlitNormal.X, geometry.SlitNormal.Y, geometry.SlitNormal.Z));
        Vector3D slitFrameUpDirection = NormalizeVector(new Vector3D(geometry.SlitUpDirection.X, geometry.SlitUpDirection.Y, geometry.SlitUpDirection.Z));
        Vector3D surfaceNormal = NormalizeVector(new Vector3D(geometry.DisplayPlaneNormal.X, geometry.DisplayPlaneNormal.Y, geometry.DisplayPlaneNormal.Z));
        Point3D surfacePoint = new Point3D(geometry.DisplayPlanePointMm.XMm, geometry.DisplayPlanePointMm.YMm, geometry.DisplayPlanePointMm.ZMm);
        Plane displaySurface = new Plane(surfaceNormal, surfacePoint);

        List<Frame> slits = BuildSlits(chronotapeFrameOrigin, slitFramesDirection, slitFrameNormal, slitFrameUpDirection, geometry);
        Vector3D surfaceUp = NormalizeVector(new Vector3D(geometry.DisplayPlaneUpDirection.X, geometry.DisplayPlaneUpDirection.Y, geometry.DisplayPlaneUpDirection.Z));
        List<Frame> displayedSegments = BuildDisplayedSegments(displaySurface, slitFramesDirection, surfaceUp, geometry);
        Point3D?[] lightSources = ComputeLightSources(slits, displayedSegments);

        for (int slitIndex = 0; slitIndex < slits.Count; slitIndex++)
        {
            if (!lightSources[slitIndex].HasValue)
            {
                continue;
            }
        }

        BuildVerificationGrid(projectedDir, characterBitmaps, slits, displayedSegments, lightSources, geometry.GlyphPixelSizeMm);
    }

    private static List<Frame> BuildSlits(Point3D origin, Vector3D direction, Vector3D normal, Vector3D up, WorldGeometryConfig geometry)
    {
        var result = new List<Frame>();
        double middleIndex = (geometry.SlitCount - 1) / 2.0;
        Vector3D topShift = ScaleVector(normal, geometry.TapeTopHeightFromGroundMm);

        for (int i = 0; i < geometry.SlitCount; i++)
        {
            double offset = (i - middleIndex) * geometry.SlitSegmentCenterDistanceMm;
            Point3D center = new Point3D(
                origin.X + (direction.X * offset) + topShift.X,
                origin.Y + (direction.Y * offset) + topShift.Y,
                origin.Z + (direction.Z * offset) + topShift.Z
            );
            result.Add(new Frame(center, normal, up, geometry.SlitWidthMm, geometry.SlitHeightMm));
        }

        return result;
    }

    private static List<Frame> BuildDisplayedSegments(Plane surface, Vector3D direction, Vector3D up, WorldGeometryConfig geometry)
    {
        var result = new List<Frame>();
        double middleIndex = (geometry.SlitCount - 1) / 2.0;

        for (int i = 0; i < geometry.SlitCount; i++)
        {
            double offset = (i - middleIndex) * geometry.DisplayedSegmentCenterDistanceMm;
            Point3D center = new Point3D(
                surface.Point.X + (direction.X * offset),
                surface.Point.Y + (direction.Y * offset),
                surface.Point.Z + (direction.Z * offset)
            );
            result.Add(new Frame(center, surface.Normal, up, geometry.DisplayedSegmentWidthMm, geometry.DisplayedSegmentHeightMm));
        }

        return result;
    }

    private static Point3D?[] ComputeLightSources(List<Frame> slits, List<Frame> displayedSegments)
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
                continue;
            }

            sources[i] = lightSource;
        }

        return sources;
    }

    private static Vector3D NormalizeVector(Vector3D vector)
    {
        double length = Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y) + (vector.Z * vector.Z));
        if (length <= 0d)
        {
            throw new InvalidOperationException("World geometry vector cannot be zero.");
        }

        return new Vector3D(vector.X / length, vector.Y / length, vector.Z / length);
    }

    private static Vector3D ScaleVector(Vector3D vector, double scalar) =>
        new Vector3D(vector.X * scalar, vector.Y * scalar, vector.Z * scalar);

    private static void SaveBoolBitmap(bool[][] bitmap, string path)
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

    private static void BuildVerificationGrid(
        string projectedDir,
        List<CharacterBitmapSample> characterBitmaps,
        List<Frame> slits,
        List<Frame> displayedSegments,
        Point3D?[] lightSources,
        double glyphPixelSizeMm)
    {
        if (characterBitmaps.Count == 0 || slits.Count == 0)
        {
            return;
        }

        int cellWidth = characterBitmaps.Max(sample => sample.BitmapWidth);
        int cellHeight = characterBitmaps.Max(sample => sample.BitmapHeight);
        int columnCount = slits.Count;
        int rowCount = characterBitmaps.Count;
        int gridWidth = cellWidth * columnCount;
        int totalSeparatorHeight = Math.Max(0, rowCount - 1) * RowSeparatorThicknessPx;
        int gridHeight = (cellHeight * rowCount) + totalSeparatorHeight;

        using var grid = new SKBitmap(gridWidth, gridHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var gridCanvas = new SKCanvas(grid);
        gridCanvas.Clear(SKColors.Black);

        for (int glyphRow = 0; glyphRow < rowCount; glyphRow++)
        {
            CharacterBitmapSample glyph = characterBitmaps[glyphRow];
            int rowTop = glyphRow * (cellHeight + RowSeparatorThicknessPx);

            for (int slitColumn = 0; slitColumn < columnCount; slitColumn++)
            {
                int colLeft = slitColumn * cellWidth;
                if (!lightSources[slitColumn].HasValue)
                {
                    DrawFrameBounds(grid, colLeft, rowTop, cellWidth, cellHeight, displayedSegments[slitColumn], new SKColor(64, 64, 64));
                    continue;
                }

                SlitProjectionResult projection = ProjectionPipeline.ProjectThroughSlitGlyphToDisplay(
                    slitColumn,
                    glyph.Pixels,
                    slits[slitColumn],
                    displayedSegments[slitColumn],
                    lightSources[slitColumn]!.Value,
                    glyphPixelSizeMm);

                DrawProjectedCell(grid, colLeft, rowTop, cellWidth, cellHeight, displayedSegments[slitColumn], projection.Points);
            }

            if (glyphRow < rowCount - 1)
            {
                int separatorStart = rowTop + cellHeight;
                for (int y = separatorStart; y < separatorStart + RowSeparatorThicknessPx; y++)
                {
                    for (int x = 0; x < gridWidth; x++)
                    {
                        grid.SetPixel(x, y, new SKColor(96, 96, 96));
                    }
                }
            }
        }

        SaveBitmap(grid, Path.Combine(projectedDir, "verification-grid.png"));
    }

    private static void DrawProjectedCell(
        SKBitmap bitmap,
        int cellLeft,
        int cellTop,
        int cellWidth,
        int cellHeight,
        Frame displayFrame,
        IReadOnlyList<ProjectedPoint> points)
    {
        (int frameLeft, int frameTop, int frameWidth, int frameHeight) = ComputeDisplayFrameRect(cellLeft, cellTop, cellWidth, cellHeight, displayFrame);

        double displayWidth = FrameMath.GetFrameWidth(displayFrame);
        double displayHeight = FrameMath.GetFrameHeight(displayFrame);
        if (displayWidth <= 0 || displayHeight <= 0)
        {
            return;
        }

        foreach (ProjectedPoint point in points)
        {
            double normalizedX = (point.DisplayLocalX + (displayWidth / 2.0)) / displayWidth;
            double normalizedY = ((displayHeight / 2.0) - point.DisplayLocalY) / displayHeight;
            int x = frameLeft + Math.Clamp((int)Math.Floor(normalizedX * frameWidth), 0, frameWidth - 1);
            int y = frameTop + Math.Clamp((int)Math.Floor(normalizedY * frameHeight), 0, frameHeight - 1);
            bitmap.SetPixel(x, y, SKColors.White);
        }

        DrawFrameBounds(bitmap, cellLeft, cellTop, cellWidth, cellHeight, displayFrame, SKColors.Lime);
    }

    private static void DrawFrameBounds(
        SKBitmap bitmap,
        int cellLeft,
        int cellTop,
        int cellWidth,
        int cellHeight,
        Frame displayFrame,
        SKColor color)
    {
        (int frameLeft, int frameTop, int frameWidth, int frameHeight) = ComputeDisplayFrameRect(cellLeft, cellTop, cellWidth, cellHeight, displayFrame);
        int frameRight = frameLeft + frameWidth - 1;
        int frameBottom = frameTop + frameHeight - 1;

        for (int x = frameLeft; x <= frameRight; x++)
        {
            bitmap.SetPixel(x, frameTop, color);
            bitmap.SetPixel(x, frameBottom, color);
        }

        for (int y = frameTop; y <= frameBottom; y++)
        {
            bitmap.SetPixel(frameLeft, y, color);
            bitmap.SetPixel(frameRight, y, color);
        }
    }

    private static (int Left, int Top, int Width, int Height) ComputeDisplayFrameRect(
        int cellLeft,
        int cellTop,
        int cellWidth,
        int cellHeight,
        Frame displayFrame)
    {
        double displayWidth = FrameMath.GetFrameWidth(displayFrame);
        double displayHeight = FrameMath.GetFrameHeight(displayFrame);
        if (displayWidth <= 0 || displayHeight <= 0)
        {
            return (cellLeft, cellTop, Math.Max(1, cellWidth), Math.Max(1, cellHeight));
        }

        int availableWidth = Math.Max(1, cellWidth - (CellPaddingPx * 2));
        int availableHeight = Math.Max(1, cellHeight - (CellPaddingPx * 2));
        double scale = Math.Min(availableWidth / displayWidth, availableHeight / displayHeight);

        int frameWidth = Math.Clamp((int)Math.Round(displayWidth * scale), 1, availableWidth);
        int frameHeight = Math.Clamp((int)Math.Round(displayHeight * scale), 1, availableHeight);
        int frameLeft = cellLeft + ((cellWidth - frameWidth) / 2);
        int frameTop = cellTop + ((cellHeight - frameHeight) / 2);
        return (frameLeft, frameTop, frameWidth, frameHeight);
    }

    private static void SaveBitmap(SKBitmap bitmap, string path)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }

    private static string Sanitize(char character)
    {
        return char.IsLetterOrDigit(character) ? character.ToString() : $"u{(int)character:X4}";
    }
}
