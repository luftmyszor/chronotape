using Phys;

// Example:
// dotnet run --project ./tape-gen/tape-gen.csproj -- --font /absolute/path/to/font.ttf --text "1234" --fontSize 200 --sampleStep 2 --out ./projection-out --slitIndex 0

const double DisplayedWidth = 150;
const double DisplayedHeight = 300;
const double DisplayedSegmentCenterDistance = 160;
const double SlitWidth = 5;
const double SlitHeight = 10;
const double SlitSegmentCenterDistance = 50;
const int SlitAmount = 4;
const double TapeTopHeightFromGround = 0;

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

// --- Local helpers ---

List<Frame> BuildSlits(Point3D origin, Vector3D direction, Vector3D normal)
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

List<Frame> BuildDisplayedSegments(Plane surface, Vector3D direction, Vector3D up)
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

void PrintFrames(string title, string itemLabel, List<Frame> frames)
{
    Console.WriteLine($"\n--- {title} ---");
    for (int i = 0; i < frames.Count; i++)
    {
        Console.WriteLine($"{itemLabel} {i}: X: {frames[i].Center.X} Y: {frames[i].Center.Y} Z: {frames[i].Center.Z}");
    }
}

Point3D?[] ComputeLightSources(List<Frame> slits, List<Frame> displayedSegments)
{
    var sources = new Point3D?[SlitAmount];
    Console.WriteLine("\n--- Light Source Positions ---");

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
            Console.WriteLine($"Warning: Could not determine light source for slit {i} — rays may be parallel.");
            continue;
        }

        sources[i] = lightSource;
        Console.WriteLine($"Slit {i}: X: {lightSource.X:F2} Y: {lightSource.Y:F2} Z: {lightSource.Z:F2}");
    }

    return sources;
}

List<SlitProjectionResult> ProjectAllSlits(ProjectionOptions options, List<SampledPixel> pixels,
    List<Frame> slits, List<Frame> displayedSegments, Point3D?[] lightSources)
{
    var results = new List<SlitProjectionResult>();

    for (int i = 0; i < SlitAmount; i++)
    {
        if (options.SlitIndex.HasValue && options.SlitIndex.Value != i) continue;

        if (!lightSources[i].HasValue)
        {
            Console.WriteLine($"Skipping slit {i}: no computed light source.");
            continue;
        }

        SlitProjectionResult result = ProjectSingleSlit(i, pixels, displayedSegments[i], slits[i], lightSources[i]!.Value);
        results.Add(result);
        Console.WriteLine($"Projected slit {i}: {result.SampleCount} points.");
    }

    return results;
}

SlitProjectionResult ProjectSingleSlit(int slitIndex, List<SampledPixel> sampledPixels,
    Frame display, Frame slit, Point3D lightSource)
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
            continue;

        Vector3D centerToIntersection = new Vector3D(slit.Center, intersection);
        double localX = Vector3D.Dot(centerToIntersection, slitRight);
        double localY = Vector3D.Dot(centerToIntersection, slitUp);

        if (Math.Abs(localX) > slitHalfWidth || Math.Abs(localY) > slitHalfHeight)
            continue;

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
