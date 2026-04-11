using System.Numerics;
using Phys;
using SkiaSharp;

internal sealed class TapeSpec
{
    public string SegmentCharacters { get; set; } = string.Empty;
    public string MainCharacters { get; set; } = string.Empty;
    public int Offset { get; set; }
    public int SlitCount { get; set; } = 1;

    public int SegmentWidthPx { get; set; }
    public int SegmentHeightPx { get; set; }
    public int TopMarginPx { get; set; }
    public int SlitWidthPx { get; set; }
    public int SlitHeightPx { get; set; }
    public int SlitCenterYOffsetPx { get; set; }

    public string? FontPath { get; set; }
    public string FontFamily { get; set; } = "Digital-7";
    public SKFontStyle FontStyle { get; set; } = SKFontStyle.Normal;
    public SKColor ForegroundColor { get; set; } = SKColors.White;
    public SKColor BackgroundColor { get; set; } = SKColors.Black;

    public int MainPaddingXPx { get; set; }
    public int MainPaddingYPx { get; set; }

    public string OutputPath { get; set; } = string.Empty;
    public bool DebugDrawRects { get; set; }
    public bool DebugHighlightRects { get; set; }
}

internal static class TapeBitmapGenerator
{
    private const char CellReferenceCharacter = '8';
    private const float MinFontSizePx = 1f;
    private const float FontSearchUpperBoundMultiplier = 4f;
    private const int FontSearchIterations = 30;
    private const float DeadzoneScaleFactor = 0.95f;
    private const float ProjectionLightDistance = 400f;
    private const float ProjectionDisplayTiltDegrees = 10f;
    private const float ProjectionBaseOffsetXRatio = 0.1f;
    private const float ProjectionSlitSpreadXRatio = 0.25f;
    private const float ProjectionBaseOffsetYRatio = 0.08f;
    private const float ProjectionEpsilon = 1e-5f;
    private const int ProjectionSampleStep = 1;
    private const float MinimumDisplayDistance = 1f;
    private const float MaxFontSearchUpperBound = 8192f;
    private const byte GlyphMaskAlphaThreshold = 16;

    public static SKBitmap GenerateTapeBitmap(TapeSpec spec)
    {
        return GenerateTapeBitmap(spec, 0);
    }

    public static SKBitmap GenerateTapeBitmap(TapeSpec spec, int slitIndex)
    {
        ValidateSpec(spec, out int segmentCount, out ReadOnlySpan<char> mainChars);
        if (slitIndex < 0 || slitIndex >= spec.SlitCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slitIndex), $"slitIndex must be between 0 and {spec.SlitCount - 1}.");
        }

        bool useFontFile = !string.IsNullOrWhiteSpace(spec.FontPath);
        using SKTypeface typeface = ResolveTypeface(spec);

        int width = spec.SegmentWidthPx;
        int height;
        checked
        {
            height = spec.TopMarginPx + (segmentCount * spec.SegmentHeightPx);
        }

        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(spec.BackgroundColor);

        for (int i = 0; i < segmentCount; i++)
        {
            SKRectI segmentRect = new(
                0,
                spec.TopMarginPx + (i * spec.SegmentHeightPx),
                spec.SegmentWidthPx,
                spec.TopMarginPx + ((i + 1) * spec.SegmentHeightPx));

            SKRectI mainRect = InsetRectOrThrow(segmentRect, spec.MainPaddingXPx, spec.MainPaddingYPx, "main glyph");
            DrawMainGlyph(canvas, mainChars[i], mainRect, typeface, spec.ForegroundColor);

            int deadzoneIndex = (i + spec.Offset) % segmentCount;
            char deadzoneChar = spec.SegmentCharacters[deadzoneIndex];
            SKRectI absoluteDeadzoneRect = ComputeDeadzoneApertureRect(segmentRect, spec);
            if (useFontFile)
            {
                DrawProjectedDeadzoneGlyphUsingPipeline(bitmap, deadzoneChar, mainRect, absoluteDeadzoneRect, typeface, spec.ForegroundColor, slitIndex, spec.SlitCount, i);
            }
            else
            {
                DrawProjectedDeadzoneGlyphLegacy(bitmap, deadzoneChar, mainRect, absoluteDeadzoneRect, typeface, spec.ForegroundColor, slitIndex, spec.SlitCount);
            }

            if (spec.DebugDrawRects)
            {
                DrawDebugRect(canvas, segmentRect, new SKColor(64, 64, 64));
                DrawDebugRect(canvas, absoluteDeadzoneRect, new SKColor(255, 0, 0));
            }

            if (spec.DebugHighlightRects)
            {
                DrawHighlightRect(canvas, segmentRect, new SKColor(255, 255, 0, 40), new SKColor(255, 255, 0), 2f);
                DrawHighlightRect(canvas, absoluteDeadzoneRect, new SKColor(255, 0, 255, 80), new SKColor(255, 0, 255), 2f);
            }
        }

        return bitmap;
    }

    public static void ExportTape(TapeSpec spec)
    {
        if (string.IsNullOrWhiteSpace(spec.OutputPath))
        {
            throw new ArgumentException("OutputPath is required.", nameof(spec));
        }

        string extension = Path.GetExtension(spec.OutputPath).ToLowerInvariant();
        SKEncodedImageFormat format = extension switch
        {
            ".png" => SKEncodedImageFormat.Png,
            ".bmp" => SKEncodedImageFormat.Bmp,
            _ => throw new ArgumentException("OutputPath must end with .png or .bmp.", nameof(spec))
        };

        string fullPath = Path.GetFullPath(spec.OutputPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (spec.SlitCount == 1)
        {
            SaveSingleBitmap(spec, format, fullPath, 0, writeSuffix: false);
            return;
        }

        for (int slitIndex = 0; slitIndex < spec.SlitCount; slitIndex++)
        {
            SaveSingleBitmap(spec, format, fullPath, slitIndex, writeSuffix: true);
        }
    }

    private static void ValidateSpec(TapeSpec spec, out int segmentCount, out ReadOnlySpan<char> mainChars)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (string.IsNullOrWhiteSpace(spec.SegmentCharacters))
        {
            throw new ArgumentException("SegmentCharacters must not be empty.", nameof(spec));
        }

        segmentCount = spec.SegmentCharacters.Length;

        if (string.IsNullOrWhiteSpace(spec.MainCharacters))
        {
            throw new ArgumentException("MainCharacters is required and must match SegmentCharacters length.", nameof(spec));
        }

        if (spec.MainCharacters.Length != segmentCount)
        {
            throw new ArgumentException(
                $"MainCharacters length ({spec.MainCharacters.Length}) must equal SegmentCharacters length ({segmentCount}).",
                nameof(spec));
        }

        if (spec.Offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(spec), "Offset must be >= 0.");
        }

        if (spec.SlitCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(spec), "SlitCount must be > 0.");
        }

        if (spec.SegmentWidthPx <= 0 || spec.SegmentHeightPx <= 0)
        {
            throw new ArgumentException("SegmentWidthPx and SegmentHeightPx must be > 0.", nameof(spec));
        }

        if (spec.TopMarginPx < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(spec), "TopMarginPx must be >= 0.");
        }

        if (spec.MainPaddingXPx < 0
            || spec.MainPaddingYPx < 0)
        {
            throw new ArgumentException("Main horizontal and vertical paddings must be >= 0.", nameof(spec));
        }

        if (spec.SlitWidthPx <= 0 || spec.SlitHeightPx <= 0)
        {
            throw new ArgumentException("SlitWidthPx and SlitHeightPx must be > 0.", nameof(spec));
        }

        if (spec.SlitWidthPx > spec.SegmentWidthPx || spec.SlitHeightPx > spec.SegmentHeightPx)
        {
            throw new ArgumentException("SlitWidthPx and SlitHeightPx must fit inside a segment rectangle.", nameof(spec));
        }

        SKRectI localApertureRect = ComputeDeadzoneApertureRect(new SKRectI(0, 0, spec.SegmentWidthPx, spec.SegmentHeightPx), spec);
        if (localApertureRect.Top < 0 || localApertureRect.Bottom > spec.SegmentHeightPx)
        {
            throw new ArgumentException("SlitCenterYOffsetPx places slit aperture outside segment bounds.", nameof(spec));
        }

        if (string.IsNullOrWhiteSpace(spec.FontFamily))
        {
            throw new ArgumentException("FontFamily must not be empty.", nameof(spec));
        }

        if (!string.IsNullOrWhiteSpace(spec.FontPath) && !File.Exists(spec.FontPath))
        {
            throw new ArgumentException($"Font file does not exist: {spec.FontPath}", nameof(spec));
        }

        mainChars = spec.MainCharacters.AsSpan();
    }

    private static SKTypeface ResolveTypeface(TapeSpec spec)
    {
        if (!string.IsNullOrWhiteSpace(spec.FontPath))
        {
            return SKTypeface.FromFile(spec.FontPath);
        }

        return SKTypeface.FromFamilyName(spec.FontFamily, spec.FontStyle);
    }

    private static void DrawMainGlyph(SKCanvas canvas, char glyph, SKRectI targetRect, SKTypeface typeface, SKColor color)
    {
        float fontSize = FindLargestFittingCellTextSize(targetRect.Width, targetRect.Height, typeface);
        DrawGlyphCenteredByCell(canvas, glyph, targetRect, typeface, color, fontSize);
    }

    private static void DrawProjectedDeadzoneGlyphLegacy(
        SKBitmap tapeBitmap,
        char glyph,
        SKRectI sourceRect,
        SKRectI deadzoneApertureRect,
        SKTypeface typeface,
        SKColor color,
        int slitIndex,
        int slitCount)
    {
        using SKBitmap sourceMask = RenderGlyphMask(glyph, sourceRect.Width, sourceRect.Height, typeface);
        using SKBitmap sourceMaskTight = CropToOpaqueBounds(sourceMask, "deadzone");

        float sourceWidth = sourceMaskTight.Width;
        float sourceHeight = sourceMaskTight.Height;

        float desiredScale = DeadzoneScaleFactor * MathF.Min(deadzoneApertureRect.Width / sourceWidth, deadzoneApertureRect.Height / sourceHeight);
        if (desiredScale <= 0f)
        {
            throw new InvalidOperationException("Deadzone projection scale is invalid.");
        }

        float displayDistance = ProjectionLightDistance * ((1f / desiredScale) - 1f);
        if (displayDistance <= MinimumDisplayDistance)
        {
            displayDistance = MinimumDisplayDistance;
        }

        float tiltRadians = ProjectionDisplayTiltDegrees * (MathF.PI / 180f);
        Vector3 displayCenter = new(0f, 0f, displayDistance);
        Vector3 displayRight = Vector3.UnitX;
        Vector3 displayUp = new(0f, MathF.Cos(tiltRadians), MathF.Sin(tiltRadians));
        Vector3 light = new(0f, 0f, -ProjectionLightDistance);
        float slitPosition = slitCount == 1 ? 0f : ((float)slitIndex / (slitCount - 1)) - 0.5f;
        float projectedOriginX = deadzoneApertureRect.MidX + (deadzoneApertureRect.Width * ProjectionBaseOffsetXRatio) + (deadzoneApertureRect.Width * ProjectionSlitSpreadXRatio * slitPosition);
        float projectedOriginY = deadzoneApertureRect.MidY - (deadzoneApertureRect.Height * ProjectionBaseOffsetYRatio);

        for (int y = 0; y < sourceMaskTight.Height; y++)
        {
            for (int x = 0; x < sourceMaskTight.Width; x++)
            {
                SKColor maskColor = sourceMaskTight.GetPixel(x, y);
                if (maskColor.Alpha == 0)
                {
                    continue;
                }

                float u = (((x + 0.5f) / sourceWidth) - 0.5f) * sourceWidth;
                float v = (0.5f - ((y + 0.5f) / sourceHeight)) * sourceHeight;
                Vector3 displayPoint = displayCenter + (displayRight * u) + (displayUp * v);

                float denominator = displayPoint.Z - light.Z;
                if (MathF.Abs(denominator) < ProjectionEpsilon)
                {
                    continue;
                }

                float t = (-light.Z) / denominator;
                float projectedX = light.X + (t * (displayPoint.X - light.X));
                float projectedY = light.Y + (t * (displayPoint.Y - light.Y));

                int targetX = (int)MathF.Round(projectedOriginX + projectedX);
                int targetY = (int)MathF.Round(projectedOriginY - projectedY);

                if (targetX < deadzoneApertureRect.Left
                    || targetX >= deadzoneApertureRect.Right
                    || targetY < deadzoneApertureRect.Top
                    || targetY >= deadzoneApertureRect.Bottom)
                {
                    continue;
                }

                tapeBitmap.SetPixel(targetX, targetY, color);
            }
        }
    }

    private static void DrawProjectedDeadzoneGlyphUsingPipeline(
        SKBitmap tapeBitmap,
        char glyph,
        SKRectI sourceRect,
        SKRectI deadzoneApertureRect,
        SKTypeface typeface,
        SKColor color,
        int slitIndex,
        int slitCount,
        int segmentIndex)
    {
        using SKBitmap sourceMask = RenderGlyphMask(glyph, sourceRect.Width, sourceRect.Height, typeface);
        List<SampledPixel> sampledPixels = SampleOpaquePixels(sourceMask, ProjectionSampleStep);
        if (sampledPixels.Count == 0)
        {
            return;
        }

        double tiltRadians = ProjectionDisplayTiltDegrees * (Math.PI / 180.0);
        var displayCenter = new Point3D(0, 0, ProjectionLightDistance);
        var displayNormal = new Vector3D(0, -Math.Sin(tiltRadians), Math.Cos(tiltRadians));
        var displayUp = new Vector3D(0, Math.Cos(tiltRadians), Math.Sin(tiltRadians));
        Frame displayFrame = new(displayCenter, displayNormal, displayUp, sourceRect.Width, sourceRect.Height);

        double slitPosition = slitCount == 1 ? 0.0 : ((double)slitIndex / (slitCount - 1)) - 0.5;
        double slitOffsetX = deadzoneApertureRect.Width * ProjectionSlitSpreadXRatio * slitPosition;
        Frame slitFrame = new(
            new Point3D(slitOffsetX, 0, 0),
            new Vector3D(0, 0, 1),
            new Vector3D(0, 1, 0),
            deadzoneApertureRect.Width,
            deadzoneApertureRect.Height);

        // Derive the light source as the convergence of rays from each display corner through the
        // corresponding slit corner. This guarantees that any pixel inside the display projects
        // inside the slit — no post-projection cropping is needed.
        var cornerRays = new List<Ray>
        {
            new Ray(displayFrame.TopRight, new Vector3D(displayFrame.TopRight, slitFrame.TopRight)),
            new Ray(displayFrame.TopLeft, new Vector3D(displayFrame.TopLeft, slitFrame.TopLeft)),
            new Ray(displayFrame.BottomRight, new Vector3D(displayFrame.BottomRight, slitFrame.BottomRight)),
            new Ray(displayFrame.BottomLeft, new Vector3D(displayFrame.BottomLeft, slitFrame.BottomLeft)),
        };
        if (!GeometryMath.GetClosestPointToRays(cornerRays, out Point3D lightSource))
        {
            throw new InvalidOperationException("Cannot compute projection light source: display and slit corners do not converge.");
        }

        Console.WriteLine($"[Segment {segmentIndex} '{glyph}' | Slit {slitIndex}]");
        Console.WriteLine($"  Display center : ({displayFrame.Center.X:F3}, {displayFrame.Center.Y:F3}, {displayFrame.Center.Z:F3})");
        Console.WriteLine($"  Slit center    : ({slitFrame.Center.X:F3}, {slitFrame.Center.Y:F3}, {slitFrame.Center.Z:F3})");
        Console.WriteLine($"  Light source   : ({lightSource.X:F3}, {lightSource.Y:F3}, {lightSource.Z:F3})");

        SlitProjectionResult projection = ProjectionPipeline.ProjectSingleSlit(
            slitIndex,
            sampledPixels,
            displayFrame,
            slitFrame,
            lightSource);

        bool[][] projectedBitmap = ProjectionPipeline.BuildSlitLocalBitmap(deadzoneApertureRect.Width, deadzoneApertureRect.Height, slitFrame, projection.Points);
        for (int y = 0; y < projectedBitmap.Length; y++)
        {
            for (int x = 0; x < projectedBitmap[y].Length; x++)
            {
                if (!projectedBitmap[y][x])
                {
                    continue;
                }

                int targetX = deadzoneApertureRect.Left + x;
                int targetY = deadzoneApertureRect.Top + y;
                tapeBitmap.SetPixel(targetX, targetY, color);
            }
        }
    }

    private static SKBitmap RenderGlyphMask(char glyph, int width, int height, SKTypeface typeface)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        float fontSize = FindLargestFittingCellTextSize(width, height, typeface);
        DrawGlyphCenteredByCell(canvas, glyph, new SKRectI(0, 0, width, height), typeface, SKColors.White, fontSize);
        return bitmap;
    }

    internal static SKBitmap CropToOpaqueBounds(SKBitmap bitmap, string glyphKind)
    {
        int minX = bitmap.Width;
        int maxX = -1;
        int minY = bitmap.Height;
        int maxY = -1;

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (!IsSignificantMaskPixel(bitmap.GetPixel(x, y)))
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            Console.WriteLine($"Warning: rendered {glyphKind} glyph mask had no opaque pixels.");
            return new SKBitmap(1, 1, bitmap.ColorType, bitmap.AlphaType);
        }

        int width = maxX - minX + 1;
        int height = maxY - minY + 1;
        var cropped = new SKBitmap(width, height, bitmap.ColorType, bitmap.AlphaType);
        using var canvas = new SKCanvas(cropped);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(bitmap, new SKRectI(minX, minY, maxX + 1, maxY + 1), new SKRectI(0, 0, width, height));
        return cropped;
    }

    private static List<SampledPixel> SampleOpaquePixels(SKBitmap bitmap, int step)
    {
        var sampled = new List<SampledPixel>();
        for (int y = 0; y < bitmap.Height; y += step)
        {
            for (int x = 0; x < bitmap.Width; x += step)
            {
                if (!IsSignificantMaskPixel(bitmap.GetPixel(x, y)))
                {
                    continue;
                }

                sampled.Add(new SampledPixel
                {
                    X = x,
                    Y = y,
                    BitmapWidth = bitmap.Width,
                    BitmapHeight = bitmap.Height
                });
            }
        }

        return sampled;
    }

    private static bool IsSignificantMaskPixel(SKColor color) => color.Alpha >= GlyphMaskAlphaThreshold;

    private static float FindLargestFittingCellTextSize(int targetWidth, int targetHeight, SKTypeface typeface)
    {
        float low = MinFontSizePx;
        float high = Math.Min(
            Math.Max(targetHeight * FontSearchUpperBoundMultiplier, MinFontSizePx),
            MaxFontSearchUpperBound);

        if (!CellFitsInTarget(MinFontSizePx, targetWidth, targetHeight, typeface))
        {
            throw new InvalidOperationException($"Glyph cell cannot fit target rectangle {targetWidth}x{targetHeight} at minimum font size {MinFontSizePx}.");
        }

        for (int i = 0; i < FontSearchIterations; i++)
        {
            float mid = (low + high) / 2f;
            if (CellFitsInTarget(mid, targetWidth, targetHeight, typeface))
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    private static bool CellFitsInTarget(float fontSize, int targetWidth, int targetHeight, SKTypeface typeface)
    {
        using var paint = CreatePaint(typeface, fontSize, SKColors.White);
        float cellWidth = paint.MeasureText(CellReferenceCharacter.ToString());
        SKFontMetrics metrics = paint.FontMetrics;
        float cellHeight = metrics.Descent - metrics.Ascent;
        return cellWidth <= targetWidth && cellHeight <= targetHeight;
    }

    private static void DrawGlyphCenteredByCell(SKCanvas canvas, char glyph, SKRectI rect, SKTypeface typeface, SKColor color, float fontSize)
    {
        using SKPaint paint = CreatePaint(typeface, fontSize, color);
        SKFontMetrics metrics = paint.FontMetrics;

        float cellWidth = paint.MeasureText(CellReferenceCharacter.ToString());
        float cellLeft = 0f;
        float cellRight = cellWidth;
        float cellTop = metrics.Ascent;
        float cellBottom = metrics.Descent;

        float cellCenterX = (cellLeft + cellRight) / 2f;
        float cellCenterY = (cellTop + cellBottom) / 2f;

        float targetCenterX = rect.MidX;
        float targetCenterY = rect.MidY;

        float drawX = targetCenterX - cellCenterX;
        float baselineY = targetCenterY - cellCenterY;

        canvas.DrawText(glyph.ToString(), drawX, baselineY, paint);
    }

    private static SKPaint CreatePaint(SKTypeface typeface, float size, SKColor color) => new()
    {
        Typeface = typeface,
        TextSize = size,
        IsAntialias = true,
        IsStroke = false,
        Color = color,
        SubpixelText = true,
        LcdRenderText = true,
        HintingLevel = SKPaintHinting.Full
    };

    private static SKRectI InsetRectOrThrow(SKRectI rect, int insetX, int insetY, string label)
    {
        SKRectI insetRect = new(rect.Left + insetX, rect.Top + insetY, rect.Right - insetX, rect.Bottom - insetY);
        if (insetRect.Width <= 0 || insetRect.Height <= 0)
        {
            throw new ArgumentException($"Horizontal/vertical padding leaves no drawable area for {label}.");
        }

        return insetRect;
    }

    private static SKRectI ComputeDeadzoneApertureRect(SKRectI segmentRect, TapeSpec spec)
    {
        int left = segmentRect.Left + ((segmentRect.Width - spec.SlitWidthPx) / 2);
        int top = segmentRect.Top + ((segmentRect.Height - spec.SlitHeightPx) / 2) + spec.SlitCenterYOffsetPx;
        return new SKRectI(left, top, left + spec.SlitWidthPx, top + spec.SlitHeightPx);
    }

    private static void SaveSingleBitmap(TapeSpec spec, SKEncodedImageFormat format, string fullPath, int slitIndex, bool writeSuffix)
    {
        string finalPath = writeSuffix
            ? BuildSlitPath(fullPath, slitIndex)
            : fullPath;

        using SKBitmap bitmap = GenerateTapeBitmap(spec, slitIndex);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(format, 100);
        using FileStream stream = File.Open(finalPath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }

    private static string BuildSlitPath(string fullPath, int slitIndex)
    {
        string directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);
        string extension = Path.GetExtension(fullPath);
        string fileName = $"{fileNameWithoutExtension}-slit-{slitIndex}{extension}";
        return Path.Combine(directory, fileName);
    }

    private static void DrawDebugRect(SKCanvas canvas, SKRectI rect, SKColor color)
    {
        using var paint = new SKPaint
        {
            IsAntialias = false,
            IsStroke = true,
            StrokeWidth = 1,
            Color = color
        };
        canvas.DrawRect(rect, paint);
    }

    private static void DrawHighlightRect(SKCanvas canvas, SKRectI rect, SKColor fillColor, SKColor strokeColor, float strokeWidth)
    {
        using var fill = new SKPaint
        {
            IsAntialias = false,
            IsStroke = false,
            Color = fillColor
        };
        canvas.DrawRect(rect, fill);

        using var stroke = new SKPaint
        {
            IsAntialias = false,
            IsStroke = true,
            StrokeWidth = strokeWidth,
            Color = strokeColor
        };
        canvas.DrawRect(rect, stroke);
    }
}
