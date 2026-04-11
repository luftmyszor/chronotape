using Phys;

internal static class ChronotapeApp
{
    private const double DisplayedWidth = 150;
    private const double DisplayedHeight = 300;
    private const double DisplayedSegmentCenterDistance = 160;
    private const double SlitWidth = 5;
    private const double SlitHeight = 10;
    private const double SlitSegmentCenterDistance = 50;
    private const int SlitAmount = 4;
    private const double TapeTopHeightFromGround = 0;

    public static void Run(string[] args)
    {
        ProjectionOptions? options = ProjectionCliParser.Parse(args);

        Point3D chronotapeFrameOrigin = new Point3D(0, 0, 0);
        Vector3D slitFramesDirection = new Vector3D(1, 0, 0);
        Vector3D slitFrameNormal = new Vector3D(0, 0, 1);

        Vector3D surfaceNormal = new Vector3D(0, 0, 1);
        Point3D surfacePoint = new Point3D(0, 0, 2000);
        Plane displaySurface = new Plane(surfaceNormal, surfacePoint);

        List<Frame> slits = BuildSlits(chronotapeFrameOrigin, slitFramesDirection, slitFrameNormal);
        PrintFrames("Slits", "Slit", slits);

        Vector3D surfaceUp = Vector3D.Cross(displaySurface.Normal, slitFramesDirection);
        List<Frame> displayedSegments = BuildDisplayedSegments(displaySurface, slitFramesDirection, surfaceUp);
        PrintFrames("Displayed Segments", "Display", displayedSegments);

        Point3D?[] lightSources = ComputeLightSources(slits, displayedSegments);

        if (options is null)
        {
            Console.WriteLine("\nNo font projection options passed. Geometry setup only.");
            return;
        }

        if (options.SlitIndex.HasValue && (options.SlitIndex.Value < 0 || options.SlitIndex.Value >= SlitAmount))
        {
            Console.WriteLine($"Invalid --slitIndex {options.SlitIndex.Value}. Expected 0..{SlitAmount - 1}.");
            return;
        }

        if (!File.Exists(options.FontPath))
        {
            Console.WriteLine($"Font file does not exist: {options.FontPath}");
            return;
        }

        List<SampledPixel> sampledPixels = TextSampler.RenderAndSampleText(options);
        if (sampledPixels.Count == 0)
        {
            Console.WriteLine("No drawable pixels sampled from text.");
            return;
        }

        List<SlitProjectionResult> results = ProjectAllSlits(options, sampledPixels, slits, displayedSegments, lightSources);
        ProjectionOutputWriter.WriteOutputs(options.OutPath, results, options.SlitIndex.HasValue);
        Console.WriteLine("\nDone.");
    }

    private static List<Frame> BuildSlits(Point3D chronotapeFrameOrigin, Vector3D slitFramesDirection, Vector3D slitFrameNormal)
    {
        var slits = new List<Frame>();
        double middleIndex = (SlitAmount - 1) / 2.0;

        for (int i = 0; i < SlitAmount; i++)
        {
            double currentOffset = (i - middleIndex) * SlitSegmentCenterDistance;
            Point3D slitCenter = new Point3D(
                chronotapeFrameOrigin.X + (slitFramesDirection.X * currentOffset),
                chronotapeFrameOrigin.Y + (slitFramesDirection.Y * currentOffset),
                chronotapeFrameOrigin.Z + (slitFramesDirection.Z * currentOffset) + TapeTopHeightFromGround
            );

            Frame newSlit = new Frame(
                slitCenter,
                slitFrameNormal,
                new Vector3D(0, 1, 0),
                SlitWidth,
                SlitHeight
            );

            slits.Add(newSlit);
        }

        return slits;
    }

    private static List<Frame> BuildDisplayedSegments(Plane displaySurface, Vector3D slitFramesDirection, Vector3D surfaceUp)
    {
        var displayedSegments = new List<Frame>();
        double middleIndex = (SlitAmount - 1) / 2.0;

        for (int i = 0; i < SlitAmount; i++)
        {
            double currentOffset = (i - middleIndex) * DisplayedSegmentCenterDistance;
            Point3D segmentCenter = new Point3D(
                displaySurface.Point.X + (slitFramesDirection.X * currentOffset),
                displaySurface.Point.Y + (slitFramesDirection.Y * currentOffset),
                displaySurface.Point.Z + (slitFramesDirection.Z * currentOffset)
            );

            Frame newSegment = new Frame(
                segmentCenter,
                displaySurface.Normal,
                surfaceUp,
                DisplayedWidth,
                DisplayedHeight
            );

            displayedSegments.Add(newSegment);
        }

        return displayedSegments;
    }

    private static void PrintFrames(string title, string itemLabel, List<Frame> frames)
    {
        Console.WriteLine($"\n--- {title} ---");
        for (int i = 0; i < frames.Count; i++)
        {
            Frame frame = frames[i];
            Console.WriteLine($"{itemLabel} {i}: X: {frame.Center.X} Y: {frame.Center.Y} Z: {frame.Center.Z}");
        }
    }

    private static Point3D?[] ComputeLightSources(List<Frame> slits, List<Frame> displayedSegments)
    {
        var lightSources = new Point3D?[SlitAmount];
        Console.WriteLine("\n--- Light Source Positions ---");

        for (int i = 0; i < SlitAmount; i++)
        {
            Frame display = displayedSegments[i];
            Frame slit = slits[i];

            var rays = new List<Ray>
            {
                new Ray(display.TopRight, new Vector3D(display.TopRight, slit.TopRight)),
                new Ray(display.TopLeft, new Vector3D(display.TopLeft, slit.TopLeft)),
                new Ray(display.BottomRight, new Vector3D(display.BottomRight, slit.BottomRight)),
                new Ray(display.BottomLeft, new Vector3D(display.BottomLeft, slit.BottomLeft))
            };

            if (!GeometryMath.GetClosestPointToRays(rays, out Point3D lightSource))
            {
                Console.WriteLine($"Warning: Could not determine light source for slit {i} — rays may be parallel.");
                continue;
            }

            lightSources[i] = lightSource;
            Console.WriteLine($"Slit {i}: X: {lightSource.X:F2} Y: {lightSource.Y:F2} Z: {lightSource.Z:F2}");
        }

        return lightSources;
    }

    private static List<SlitProjectionResult> ProjectAllSlits(
        ProjectionOptions options,
        List<SampledPixel> sampledPixels,
        List<Frame> slits,
        List<Frame> displayedSegments,
        Point3D?[] lightSources)
    {
        var results = new List<SlitProjectionResult>();

        for (int i = 0; i < SlitAmount; i++)
        {
            if (options.SlitIndex.HasValue && options.SlitIndex.Value != i)
            {
                continue;
            }

            if (!lightSources[i].HasValue)
            {
                Console.WriteLine($"Skipping slit {i}: no computed light source.");
                continue;
            }

            Frame display = displayedSegments[i];
            Frame slit = slits[i];
            Point3D lightSource = lightSources[i]!.Value;
            SlitProjectionResult result = ProjectSingleSlit(i, sampledPixels, display, slit, lightSource);
            results.Add(result);
            Console.WriteLine($"Projected slit {i}: {result.SampleCount} points.");
        }

        return results;
    }

    private static SlitProjectionResult ProjectSingleSlit(
        int slitIndex,
        List<SampledPixel> sampledPixels,
        Frame display,
        Frame slit,
        Point3D lightSource)
    {
        Plane slitPlane = new Plane(FrameMath.GetFrameNormal(slit), slit.Center);
        Vector3D displayRight = FrameMath.GetFrameRight(display);
        Vector3D displayUp = FrameMath.GetFrameUp(display);
        double displayWidth = FrameMath.GetFrameWidth(display);
        double displayHeight = FrameMath.GetFrameHeight(display);

        Vector3D slitRight = FrameMath.GetFrameRight(slit);
        Vector3D slitUp = FrameMath.GetFrameUp(slit);
        double slitHalfWidth = FrameMath.GetFrameWidth(slit) / 2.0;
        double slitHalfHeight = FrameMath.GetFrameHeight(slit) / 2.0;

        var projectedPoints = new List<ProjectedPoint>();
        foreach (SampledPixel pixel in sampledPixels)
        {
            double u = (((pixel.X + 0.5) / pixel.BitmapWidth) - 0.5) * displayWidth;
            double v = (0.5 - ((pixel.Y + 0.5) / pixel.BitmapHeight)) * displayHeight;
            Point3D displayPoint = FrameMath.OffsetPoint(display.Center, displayRight, u, displayUp, v);

            if (!GeometryMath.GetProjectionPoint(lightSource, displayPoint, slitPlane, out Point3D intersection))
            {
                continue;
            }

            Vector3D centerToIntersection = new Vector3D(slit.Center, intersection);
            double localX = Vector3D.Dot(centerToIntersection, slitRight);
            double localY = Vector3D.Dot(centerToIntersection, slitUp);

            if (Math.Abs(localX) > slitHalfWidth || Math.Abs(localY) > slitHalfHeight)
            {
                continue;
            }

            projectedPoints.Add(new ProjectedPoint
            {
                PixelX = pixel.X,
                PixelY = pixel.Y,
                DisplayWorldX = displayPoint.X,
                DisplayWorldY = displayPoint.Y,
                DisplayWorldZ = displayPoint.Z,
                WorldX = intersection.X,
                WorldY = intersection.Y,
                WorldZ = intersection.Z,
                SlitLocalX = localX,
                SlitLocalY = localY
            });
        }

        return new SlitProjectionResult
        {
            SlitIndex = slitIndex,
            LightSource = lightSource,
            SampleCount = projectedPoints.Count,
            Points = projectedPoints
        };
    }
}
