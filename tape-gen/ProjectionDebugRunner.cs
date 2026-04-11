using Phys;
using SkiaSharp;

internal static class ProjectionDebugRunner
{
    private const double DisplayedWidth = 150;
    private const double DisplayedHeight = 300;
    private const double DisplayedSegmentCenterDistance = 160;
    private const double SlitWidth = 5;
    private const double SlitHeight = 10;
    private const double SlitSegmentCenterDistance = 50;
    private const int SlitAmount = 4;
    private const double TapeTopHeightFromGround = 0;

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

        Point3D chronotapeFrameOrigin = new Point3D(0, 0, 0);
        Vector3D slitFramesDirection = new Vector3D(1, 0, 0);
        Vector3D slitFrameNormal = new Vector3D(0, 0, 1);
        Vector3D surfaceNormal = new Vector3D(0, 0, 1);
        Point3D surfacePoint = new Point3D(0, 0, 2000);
        Plane displaySurface = new Plane(surfaceNormal, surfacePoint);

        List<Frame> slits = BuildSlits(chronotapeFrameOrigin, slitFramesDirection, slitFrameNormal);
        Vector3D surfaceUp = Vector3D.Cross(displaySurface.Normal, slitFramesDirection);
        List<Frame> displayedSegments = BuildDisplayedSegments(displaySurface, slitFramesDirection, surfaceUp);
        Point3D?[] lightSources = ComputeLightSources(slits, displayedSegments);

        for (int slitIndex = 0; slitIndex < SlitAmount; slitIndex++)
        {
            if (!lightSources[slitIndex].HasValue)
            {
                continue;
            }

            string slitDir = Path.Combine(projectedDir, $"slit-{slitIndex}");
            Directory.CreateDirectory(slitDir);
            for (int charIndex = 0; charIndex < characterBitmaps.Count; charIndex++)
            {
                CharacterBitmapSample sample = characterBitmaps[charIndex];
                SlitProjectionResult result = ProjectionPipeline.ProjectSingleSlit(
                    slitIndex,
                    sample.Pixels,
                    displayedSegments[slitIndex],
                    slits[slitIndex],
                    lightSources[slitIndex]!.Value);

                bool[][] projectedBitmap = ProjectionPipeline.BuildSourceBitmap(sample.BitmapWidth, sample.BitmapHeight, result.Points);
                SaveBoolBitmap(projectedBitmap, Path.Combine(slitDir, $"{charIndex:D2}-{Sanitize(sample.Character)}.png"));
            }
        }
    }

    private static List<Frame> BuildSlits(Point3D origin, Vector3D direction, Vector3D normal)
    {
        var result = new List<Frame>();
        double middleIndex = (SlitAmount - 1) / 2.0;

        for (int i = 0; i < SlitAmount; i++)
        {
            double offset = (i - middleIndex) * SlitSegmentCenterDistance;
            Point3D center = new Point3D(
                origin.X + (direction.X * offset),
                origin.Y + (direction.Y * offset),
                origin.Z + (direction.Z * offset) + TapeTopHeightFromGround
            );
            result.Add(new Frame(center, normal, new Vector3D(0, 1, 0), SlitWidth, SlitHeight));
        }

        return result;
    }

    private static List<Frame> BuildDisplayedSegments(Plane surface, Vector3D direction, Vector3D up)
    {
        var result = new List<Frame>();
        double middleIndex = (SlitAmount - 1) / 2.0;

        for (int i = 0; i < SlitAmount; i++)
        {
            double offset = (i - middleIndex) * DisplayedSegmentCenterDistance;
            Point3D center = new Point3D(
                surface.Point.X + (direction.X * offset),
                surface.Point.Y + (direction.Y * offset),
                surface.Point.Z + (direction.Z * offset)
            );
            result.Add(new Frame(center, surface.Normal, up, DisplayedWidth, DisplayedHeight));
        }

        return result;
    }

    private static Point3D?[] ComputeLightSources(List<Frame> slits, List<Frame> displayedSegments)
    {
        var sources = new Point3D?[SlitAmount];

        for (int i = 0; i < SlitAmount; i++)
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

    private static string Sanitize(char character)
    {
        return char.IsLetterOrDigit(character) ? character.ToString() : $"u{(int)character:X4}";
    }
}
