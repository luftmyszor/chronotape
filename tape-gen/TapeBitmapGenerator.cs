using System.Numerics;
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
    public SKRectI DeadzoneRectPx { get; set; }

    public string FontFamily { get; set; } = "Digital-7";
    public SKFontStyle FontStyle { get; set; } = SKFontStyle.Normal;
    public SKColor ForegroundColor { get; set; } = SKColors.White;
    public SKColor BackgroundColor { get; set; } = SKColors.Black;

    public int MainPaddingPx { get; set; }
    public int DeadzonePaddingPx { get; set; }

    public string OutputPath { get; set; } = string.Empty;
    public bool DebugDrawRects { get; set; }
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

        using SKTypeface typeface = ResolveTypeface(spec.FontFamily, spec.FontStyle);

        int width = spec.SegmentWidthPx;
        int height = checked(spec.TopMarginPx + (segmentCount * spec.SegmentHeightPx));

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

            SKRectI mainRect = InsetRectOrThrow(segmentRect, spec.MainPaddingPx, "main glyph");
            DrawMainGlyph(canvas, mainChars[i], mainRect, typeface, spec.ForegroundColor);

            int deadzoneIndex = (i + spec.Offset) % segmentCount;
            char deadzoneChar = spec.SegmentCharacters[deadzoneIndex];
            SKRectI absoluteDeadzoneRect = OffsetRect(spec.DeadzoneRectPx, segmentRect.Left, segmentRect.Top);
            SKRectI deadzoneInnerRect = InsetRectOrThrow(absoluteDeadzoneRect, spec.DeadzonePaddingPx, "deadzone glyph");
            DrawProjectedDeadzoneGlyph(bitmap, deadzoneChar, mainRect, deadzoneInnerRect, typeface, spec.ForegroundColor, slitIndex, spec.SlitCount);

            if (spec.DebugDrawRects)
            {
                DrawDebugRect(canvas, segmentRect, new SKColor(64, 64, 64));
                DrawDebugRect(canvas, absoluteDeadzoneRect, new SKColor(255, 0, 0));
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

        if (spec.MainPaddingPx < 0 || spec.DeadzonePaddingPx < 0)
        {
            throw new ArgumentException("MainPaddingPx and DeadzonePaddingPx must be >= 0.", nameof(spec));
        }

        if (spec.DeadzoneRectPx.Width <= 0 || spec.DeadzoneRectPx.Height <= 0)
        {
            throw new ArgumentException("DeadzoneRectPx must have positive width and height.", nameof(spec));
        }

        if (spec.DeadzoneRectPx.Left < 0
            || spec.DeadzoneRectPx.Top < 0
            || spec.DeadzoneRectPx.Right > spec.SegmentWidthPx
            || spec.DeadzoneRectPx.Bottom > spec.SegmentHeightPx)
        {
            throw new ArgumentException("DeadzoneRectPx must fit inside a segment rectangle.", nameof(spec));
        }

        if (string.IsNullOrWhiteSpace(spec.FontFamily))
        {
            throw new ArgumentException("FontFamily must not be empty.", nameof(spec));
        }

        mainChars = spec.MainCharacters.AsSpan();
    }

    private static SKTypeface ResolveTypeface(string fontFamily, SKFontStyle style)
    {
        return SKTypeface.FromFamilyName(fontFamily, style);
    }

    private static void DrawMainGlyph(SKCanvas canvas, char glyph, SKRectI targetRect, SKTypeface typeface, SKColor color)
    {
        float fontSize = FindLargestFittingCellTextSize(targetRect.Width, targetRect.Height, typeface);
        DrawGlyphCenteredByCell(canvas, glyph, targetRect, typeface, color, fontSize);
    }

    private static void DrawProjectedDeadzoneGlyph(
        SKBitmap tapeBitmap,
        char glyph,
        SKRectI sourceRect,
        SKRectI deadzoneRect,
        SKTypeface typeface,
        SKColor color,
        int slitIndex,
        int slitCount)
    {
        using SKBitmap sourceMask = RenderGlyphMask(glyph, sourceRect.Width, sourceRect.Height, typeface);

        float sourceWidth = sourceMask.Width;
        float sourceHeight = sourceMask.Height;

        float desiredScale = DeadzoneScaleFactor * MathF.Min(deadzoneRect.Width / sourceWidth, deadzoneRect.Height / sourceHeight);
        if (desiredScale <= 0f)
        {
            throw new InvalidOperationException("Deadzone projection scale is invalid.");
        }

        float displayDistance = ProjectionLightDistance * ((1f / desiredScale) - 1f);
        if (displayDistance <= 1f)
        {
            displayDistance = 1f;
        }

        float tiltRadians = ProjectionDisplayTiltDegrees * (MathF.PI / 180f);
        Vector3 displayCenter = new(0f, 0f, displayDistance);
        Vector3 displayRight = Vector3.UnitX;
        Vector3 displayUp = new(0f, MathF.Cos(tiltRadians), MathF.Sin(tiltRadians));
        Vector3 light = new(0f, 0f, -ProjectionLightDistance);
        float slitPosition = slitCount == 1 ? 0f : ((float)slitIndex / (slitCount - 1)) - 0.5f;
        float projectedOriginX = deadzoneRect.MidX + (deadzoneRect.Width * ProjectionBaseOffsetXRatio) + (deadzoneRect.Width * ProjectionSlitSpreadXRatio * slitPosition);
        float projectedOriginY = deadzoneRect.MidY - (deadzoneRect.Height * ProjectionBaseOffsetYRatio);

        for (int y = 0; y < sourceMask.Height; y++)
        {
            for (int x = 0; x < sourceMask.Width; x++)
            {
                SKColor maskColor = sourceMask.GetPixel(x, y);
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

                if (targetX < deadzoneRect.Left
                    || targetX >= deadzoneRect.Right
                    || targetY < deadzoneRect.Top
                    || targetY >= deadzoneRect.Bottom)
                {
                    continue;
                }

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

    private static float FindLargestFittingCellTextSize(int targetWidth, int targetHeight, SKTypeface typeface)
    {
        float low = MinFontSizePx;
        float high = Math.Max(targetHeight * FontSearchUpperBoundMultiplier, MinFontSizePx);

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

    private static SKRectI InsetRectOrThrow(SKRectI rect, int inset, string label)
    {
        SKRectI insetRect = new(rect.Left + inset, rect.Top + inset, rect.Right - inset, rect.Bottom - inset);
        if (insetRect.Width <= 0 || insetRect.Height <= 0)
        {
            throw new ArgumentException($"Padding leaves no drawable area for {label}.");
        }

        return insetRect;
    }

    private static SKRectI OffsetRect(SKRectI rect, int dx, int dy) =>
        new(rect.Left + dx, rect.Top + dy, rect.Right + dx, rect.Bottom + dy);

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
}
