using SkiaSharp;

internal static class TextSampler
{
    public static List<SampledPixel> RenderAndSampleText(ProjectionOptions options)
    {
        using var typeface = SKTypeface.FromFile(options.FontPath);
        if (typeface is null)
        {
            throw new InvalidOperationException($"Unable to load font file: {options.FontPath}");
        }

        using var paint = new SKPaint
        {
            Typeface = typeface,
            TextSize = (float)options.FontSize,
            IsAntialias = true,
            Color = SKColors.White,
            IsStroke = false
        };

        SKRect bounds = default;
        paint.MeasureText(options.Text, ref bounds);

        const int padding = 8;
        int width = Math.Max(1, (int)Math.Ceiling(bounds.Width) + (padding * 2));
        int height = Math.Max(1, (int)Math.Ceiling(bounds.Height) + (padding * 2));

        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        float drawX = padding - bounds.Left;
        float drawY = padding - bounds.Top;
        canvas.DrawText(options.Text, drawX, drawY, paint);
        canvas.Flush();

        bool edgeOnly = string.Equals(options.SampleMode, "edge", StringComparison.OrdinalIgnoreCase);
        var sampled = new List<SampledPixel>();

        for (int y = 0; y < bitmap.Height; y += options.SampleStep)
        {
            for (int x = 0; x < bitmap.Width; x += options.SampleStep)
            {
                if (bitmap.GetPixel(x, y).Alpha == 0)
                {
                    continue;
                }

                if (edgeOnly && !IsEdgePixel(bitmap, x, y))
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

        Console.WriteLine($"Sampled {sampled.Count} pixels from rendered text ({bitmap.Width}x{bitmap.Height}, mode={options.SampleMode}, step={options.SampleStep}).");
        return sampled;
    }

    private static bool IsEdgePixel(SKBitmap bitmap, int x, int y)
    {
        if (x == 0 || y == 0 || x == bitmap.Width - 1 || y == bitmap.Height - 1)
        {
            return true;
        }

        return bitmap.GetPixel(x - 1, y).Alpha == 0 ||
               bitmap.GetPixel(x + 1, y).Alpha == 0 ||
               bitmap.GetPixel(x, y - 1).Alpha == 0 ||
               bitmap.GetPixel(x, y + 1).Alpha == 0;
    }
}
