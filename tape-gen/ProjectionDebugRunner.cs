using Phys;
using SkiaSharp;

internal static class ProjectionDebugRunner
{
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

    private static List<Frame> BuildSlits(Point3D origin, Vector3D direction, Vector3D normal, Vector3D up, WorldGeometryConfig geometry)
    {
        var result = new List<Frame>();
        double middleIndex = (geometry.SlitCount - 1) / 2.0;
        Vector3D topShift = ScaleVector(normal, geometry.TapeTopHeightFromGroundMm);
        Vector3D centerYShift = ScaleVector(up, geometry.SlitCenterYOffsetMm);

        for (int i = 0; i < geometry.SlitCount; i++)
        {
            double offset = (i - middleIndex) * geometry.SlitSegmentCenterDistanceMm;
            Point3D center = new Point3D(
                origin.X + (direction.X * offset) + topShift.X + centerYShift.X,
                origin.Y + (direction.Y * offset) + topShift.Y + centerYShift.Y,
                origin.Z + (direction.Z * offset) + topShift.Z + centerYShift.Z
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

    private static string Sanitize(char character)
    {
        return char.IsLetterOrDigit(character) ? character.ToString() : $"u{(int)character:X4}";
    }
}
