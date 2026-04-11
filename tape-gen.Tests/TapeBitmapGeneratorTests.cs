using SkiaSharp;
using Xunit;

public sealed class TapeBitmapGeneratorTests
{
    private const byte MaxGreenForMagenta = 30;
    private const byte MinRedOrBlueForMagenta = 40;
    private const byte MaxRedOrBlueForMagenta = 200;

    [Fact]
    public void GenerateTapeBitmap_UsesDeadzoneApertureForProjectionAndAppliesPaddingAsClip()
    {
        var baselineSpec = new TapeSpec
        {
            SegmentCharacters = "8",
            MainCharacters = "8",
            Offset = 0,
            SlitCount = 1,
            SegmentWidthPx = 140,
            SegmentHeightPx = 210,
            TopMarginPx = 0,
            SlitWidthPx = 36,
            SlitHeightPx = 36,
            SlitCenterYOffsetPx = 61,
            FontFamily = "monospace",
            FontStyle = SKFontStyle.Normal,
            ForegroundColor = SKColors.White,
            BackgroundColor = SKColors.Black,
            MainPaddingXPx = 8,
            MainPaddingYPx = 8,
            DeadzonePaddingXPx = 0,
            DeadzonePaddingYPx = 0
        };

        var clippedSpec = new TapeSpec
        {
            SegmentCharacters = baselineSpec.SegmentCharacters,
            MainCharacters = baselineSpec.MainCharacters,
            Offset = baselineSpec.Offset,
            SlitCount = baselineSpec.SlitCount,
            SegmentWidthPx = baselineSpec.SegmentWidthPx,
            SegmentHeightPx = baselineSpec.SegmentHeightPx,
            TopMarginPx = baselineSpec.TopMarginPx,
            SlitWidthPx = baselineSpec.SlitWidthPx,
            SlitHeightPx = baselineSpec.SlitHeightPx,
            SlitCenterYOffsetPx = baselineSpec.SlitCenterYOffsetPx,
            FontFamily = baselineSpec.FontFamily,
            FontStyle = baselineSpec.FontStyle,
            ForegroundColor = baselineSpec.ForegroundColor,
            BackgroundColor = baselineSpec.BackgroundColor,
            MainPaddingXPx = baselineSpec.MainPaddingXPx,
            MainPaddingYPx = baselineSpec.MainPaddingYPx,
            DeadzonePaddingXPx = 4,
            DeadzonePaddingYPx = 4
        };

        using SKBitmap baseline = TapeBitmapGenerator.GenerateTapeBitmap(baselineSpec);
        using SKBitmap clipped = TapeBitmapGenerator.GenerateTapeBitmap(clippedSpec);

        SKRectI apertureRect = BuildApertureRect(baselineSpec);
        SKRectI clipRect = new(
            apertureRect.Left + clippedSpec.DeadzonePaddingXPx,
            apertureRect.Top + clippedSpec.DeadzonePaddingYPx,
            apertureRect.Right - clippedSpec.DeadzonePaddingXPx,
            apertureRect.Bottom - clippedSpec.DeadzonePaddingYPx);

        for (int y = clipRect.Top; y < clipRect.Bottom; y++)
        {
            for (int x = clipRect.Left; x < clipRect.Right; x++)
            {
                Assert.Equal(baseline.GetPixel(x, y), clipped.GetPixel(x, y));
            }
        }
    }

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
        DeadzonePaddingXPx = 0,
        DeadzonePaddingYPx = 0,
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
}
