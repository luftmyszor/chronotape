using SkiaSharp;
using Xunit;

public sealed class TapeBitmapGeneratorTests
{
    private const byte MaxGreenForMagenta = 30;
    private const byte MinRedOrBlueForMagenta = 40;
    private const byte MaxRedOrBlueForMagenta = 200;

    [Fact]
    public void GenerateTapeBitmap_CentersHorizontallyAndMovesApertureVerticallyByOffset()
    {
        TapeSpec topSpec = BuildDebugHighlightSpec(slitWidthPx: 36, slitHeightPx: 30, slitCenterYOffsetPx: 40);
        TapeSpec bottomSpec = BuildDebugHighlightSpec(slitWidthPx: 36, slitHeightPx: 30, slitCenterYOffsetPx: 60);

        using SKBitmap topBitmap = TapeBitmapGenerator.GenerateTapeBitmap(topSpec);
        using SKBitmap bottomBitmap = TapeBitmapGenerator.GenerateTapeBitmap(bottomSpec);

        SKRectI topBounds = FindDeadzoneHighlightBounds(topBitmap);
        SKRectI bottomBounds = FindDeadzoneHighlightBounds(bottomBitmap);

        Assert.Equal(topBounds.Width, bottomBounds.Width);
        Assert.Equal(topBounds.Height, bottomBounds.Height);
        Assert.Equal(topBounds.Left, bottomBounds.Left);
        Assert.Equal(20, bottomBounds.Top - topBounds.Top);
    }

    [Fact]
    public void GenerateTapeBitmap_UsesSlitDimensionsAsApertureSize()
    {
        TapeSpec narrowSpec = BuildDebugHighlightSpec(slitWidthPx: 20, slitHeightPx: 24, slitCenterYOffsetPx: 50);
        TapeSpec wideSpec = BuildDebugHighlightSpec(slitWidthPx: 40, slitHeightPx: 34, slitCenterYOffsetPx: 50);

        using SKBitmap narrowBitmap = TapeBitmapGenerator.GenerateTapeBitmap(narrowSpec);
        using SKBitmap wideBitmap = TapeBitmapGenerator.GenerateTapeBitmap(wideSpec);

        SKRectI narrowBounds = FindDeadzoneHighlightBounds(narrowBitmap);
        SKRectI wideBounds = FindDeadzoneHighlightBounds(wideBitmap);

        Assert.Equal(18, narrowBounds.Width);
        Assert.Equal(22, narrowBounds.Height);
        Assert.Equal(38, wideBounds.Width);
        Assert.Equal(32, wideBounds.Height);
        Assert.Equal(((narrowSpec.SegmentWidthPx - 20) / 2) + 1, narrowBounds.Left);
        Assert.Equal(((wideSpec.SegmentWidthPx - 40) / 2) + 1, wideBounds.Left);
    }

    [Fact]
    public void GenerateTapeBitmap_LogsProjectionGeometryOncePerSlit()
    {
        TapeSpec spec = BuildDebugHighlightSpec(slitWidthPx: 36, slitHeightPx: 30, slitCenterYOffsetPx: 50);
        spec.SegmentCharacters = "1234";
        spec.MainCharacters = "1234";
        spec.SlitCount = 4;
        spec.FontPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../tape-gen/digital-7.regular.ttf"));

        TextWriter originalOut = Console.Out;
        using var capture = new StringWriter();
        try
        {
            Console.SetOut(capture);
            using SKBitmap _ = TapeBitmapGenerator.GenerateTapeBitmap(spec, slitIndex: 2);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        string output = capture.ToString();
        Assert.Equal(1, CountSubstring(output, "[Slit 2]"));
        Assert.Equal(1, CountSubstring(output, "Display center"));
        Assert.Equal(1, CountSubstring(output, "Slit center"));
        Assert.Equal(1, CountSubstring(output, "Light source"));
    }

    [Fact]
    public void GenerateTapeBitmap_DisplayCenterDiffersAcrossSlits()
    {
        TapeSpec spec = BuildDebugHighlightSpec(slitWidthPx: 36, slitHeightPx: 30, slitCenterYOffsetPx: 50);
        spec.SegmentCharacters = "1234";
        spec.MainCharacters = "1234";
        spec.SlitCount = 4;
        spec.FontPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../tape-gen/digital-7.regular.ttf"));

        var displayCenters = new List<string>();
        for (int slitIndex = 0; slitIndex < spec.SlitCount; slitIndex++)
        {
            TextWriter originalOut = Console.Out;
            using var capture = new StringWriter();
            try
            {
                Console.SetOut(capture);
                using SKBitmap _ = TapeBitmapGenerator.GenerateTapeBitmap(spec, slitIndex);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            string output = capture.ToString();
            string? centerLine = output.Split('\n').FirstOrDefault(l => l.Contains("Display center"));
            Assert.NotNull(centerLine);
            displayCenters.Add(centerLine!.Trim());
        }

        // Every slit must have a distinct display-center line
        Assert.Equal(spec.SlitCount, displayCenters.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void CropToOpaqueBounds_IgnoresLowAlphaFringePixels()
    {
        using var bitmap = new SKBitmap(10, 10, SKColorType.Bgra8888, SKAlphaType.Premul);
        bitmap.Erase(SKColors.Transparent);
        bitmap.SetPixel(0, 0, new SKColor(255, 255, 255, 1));
        for (int y = 4; y <= 6; y++)
        {
            for (int x = 4; x <= 6; x++)
            {
                bitmap.SetPixel(x, y, SKColors.White);
            }
        }

        using SKBitmap cropped = TapeBitmapGenerator.CropToOpaqueBounds(bitmap, "test");
        Assert.Equal(3, cropped.Width);
        Assert.Equal(3, cropped.Height);
    }

    private static SKRectI BuildApertureRect(TapeSpec spec)
    {
        int left = (spec.SegmentWidthPx - spec.SlitWidthPx) / 2;
        int top = ((spec.SegmentHeightPx - spec.SlitHeightPx) / 2) + spec.SlitCenterYOffsetPx;
        return new SKRectI(left, top, left + spec.SlitWidthPx, top + spec.SlitHeightPx);
    }

    private static TapeSpec BuildDebugHighlightSpec(int slitWidthPx, int slitHeightPx, int slitCenterYOffsetPx) => new()
    {
        SegmentCharacters = "8",
        MainCharacters = "8",
        Offset = 0,
        SlitCount = 1,
        SegmentWidthPx = 140,
        SegmentHeightPx = 210,
        TopMarginPx = 0,
        SlitWidthPx = slitWidthPx,
        SlitHeightPx = slitHeightPx,
        SlitCenterYOffsetPx = slitCenterYOffsetPx,
        FontFamily = "monospace",
        FontStyle = SKFontStyle.Normal,
        ForegroundColor = SKColors.Black,
        BackgroundColor = SKColors.Black,
        MainPaddingXPx = 8,
        MainPaddingYPx = 8,
        DebugHighlightRects = true
    };

    private static SKRectI FindDeadzoneHighlightBounds(SKBitmap bitmap)
    {
        int left = int.MaxValue;
        int top = int.MaxValue;
        int rightExclusive = int.MinValue;
        int bottomExclusive = int.MinValue;

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                SKColor color = bitmap.GetPixel(x, y);
                bool isMagentaLike = color.Green < MaxGreenForMagenta
                    && color.Red > MinRedOrBlueForMagenta
                    && color.Red < MaxRedOrBlueForMagenta
                    && color.Blue > MinRedOrBlueForMagenta
                    && color.Blue < MaxRedOrBlueForMagenta;
                if (!isMagentaLike)
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                rightExclusive = Math.Max(rightExclusive, x + 1);
                bottomExclusive = Math.Max(bottomExclusive, y + 1);
            }
        }

        return new SKRectI(left, top, rightExclusive, bottomExclusive);
    }

    private static SKRectI FindOpaqueBounds(SKBitmap bitmap)
    {
        int left = int.MaxValue;
        int top = int.MaxValue;
        int rightExclusive = int.MinValue;
        int bottomExclusive = int.MinValue;

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                SKColor color = bitmap.GetPixel(x, y);
                if (color.Red == 0 && color.Green == 0 && color.Blue == 0)
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                rightExclusive = Math.Max(rightExclusive, x + 1);
                bottomExclusive = Math.Max(bottomExclusive, y + 1);
            }
        }

        return new SKRectI(left, top, rightExclusive, bottomExclusive);
    }

    private static int CountSubstring(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
