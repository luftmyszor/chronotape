using SkiaSharp;

internal static class TextSampler
{
    public static List<CharacterBitmapSample> RenderAndSampleCharacters(string fontPath, string text, int textSize, int sampleStep)
    {
        using var typeface = SKTypeface.FromFile(fontPath);
        if (typeface is null)
        {
            throw new InvalidOperationException($"Unable to load font file: {fontPath}");
        }

        using var paint = new SKPaint
        {
            Typeface = typeface,
            TextSize = textSize,
            IsAntialias = true,
            Color = SKColors.White,
            IsStroke = false
        };

        var characterBitmaps = new List<CharacterBitmapSample>(text.Length);
        foreach (char character in text)
        {
            characterBitmaps.Add(RenderAndSampleCharacter(character, paint, sampleStep));
        }

        return characterBitmaps;
    }

    private static CharacterBitmapSample RenderAndSampleCharacter(char character, SKPaint paint, int sampleStep)
    {
        // 1. Get Font Metrics for consistent vertical sizing
        SKFontMetrics metrics;
        paint.GetFontMetrics(out metrics);

        // Height = Ascent (negative) to Descent (positive)
        // We use Abs() because Ascent is usually a negative value from the baseline
        int commonHeight = (int)Math.Ceiling(Math.Abs(metrics.Ascent) + Math.Abs(metrics.Descent) / 2);

        // 2. Set a fixed width. 
        // Option A: Use the Max Character Width of the font.
        // Option B: Measure a wide character like 'W' or '8' once and reuse it.
        int commonWidth = (int)Math.Ceiling(paint.MeasureText("8"));

        // 3. Create the bitmap with fixed dimensions
        using var bitmap = new SKBitmap(commonWidth, commonHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        // 4. Calculate Draw Position
        // drawX: 0 centers the text to the left; you could add offset to center horizontally
        // drawY: must be the absolute value of Ascent to place the baseline correctly
        float drawX = 0;
        float drawY = Math.Abs(metrics.Ascent);

        canvas.DrawText(character.ToString(), drawX, drawY, paint);
        canvas.Flush();

        var sampled = new List<SampledPixel>();

        for (int y = 0; y < bitmap.Height; y += sampleStep)
        {
            for (int x = 0; x < bitmap.Width; x += sampleStep)
            {
                if (bitmap.GetPixel(x, y).Alpha == 0) continue;

                sampled.Add(new SampledPixel
                {
                    X = x,
                    Y = y,
                    BitmapWidth = bitmap.Width,
                    BitmapHeight = bitmap.Height
                });
            }
        }

        return new CharacterBitmapSample
        {
            Character = character,
            BitmapWidth = commonWidth,
            BitmapHeight = commonHeight,
            Pixels = sampled
        };
    }
}
