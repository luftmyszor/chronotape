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
        SKRect bounds = default;
        paint.MeasureText(character.ToString(), ref bounds);

        const int padding = 8;
        int width = Math.Max(1, (int)Math.Ceiling(bounds.Width) + (padding * 2));
        int height = Math.Max(1, (int)Math.Ceiling(bounds.Height) + (padding * 2));

        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        float drawX = padding - bounds.Left;
        float drawY = padding - bounds.Top;
        canvas.DrawText(character.ToString(), drawX, drawY, paint);
        canvas.Flush();

        var sampled = new List<SampledPixel>();

        for (int y = 0; y < bitmap.Height; y += sampleStep)
        {
            for (int x = 0; x < bitmap.Width; x += sampleStep)
            {
                if (bitmap.GetPixel(x, y).Alpha == 0)
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

        Console.WriteLine($"Sampled {sampled.Count} pixels from rendered character '{character}' ({bitmap.Width}x{bitmap.Height}, step={sampleStep}).");
        return new CharacterBitmapSample
        {
            Character = character,
            BitmapWidth = width,
            BitmapHeight = height,
            Pixels = sampled
        };
    }
}
