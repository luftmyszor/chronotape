using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Phys;
using SkiaSharp;

const double DISPLAYED_WIDTH = 150;
const double DISPLAYED_HEIGHT = 300;
const double DISPLAYED_SEGMENT_CENTER_DISTANCE = 160;

const double SLIT_WIDTH = 5;
const double SLIT_HEIGHT = 10;
const double SLIT_SEGMENT_CENTER_DISTANCE = 50;

const int SLIT_AMOUNT = 4;

const double TAPE_TOP_HEIGHT_FROM_GROUND = 0;

// Example:
// dotnet run --project ./tape-gen/tape-gen.csproj -- --font /absolute/path/to/font.ttf --text "12:34" --fontSize 200 --sampleStep 2 --out ./projection-out --slitIndex 0

ProjectionOptions? options = ParseProjectionOptions(args);

Point3D chronotapeFrameOrigin = new Point3D(0, 0, 0);
Vector3D slitFramesDirection = new Vector3D(1, 0, 0);
Vector3D slitFrameNormal = new Vector3D(0, 0, 1);

Vector3D surfaceNormal = new Vector3D(0, 0, 1);
Point3D surfacePoint = new Point3D(0, 0, 2000);
Plane displaySurface = new Plane(surfaceNormal, surfacePoint);

var slits = new List<Frame>();
// Calculate the exact middle index of the slit sequence
// For 4 slits, this is (4 - 1) / 2.0 = 1.5
double middleIndex = (SLIT_AMOUNT - 1) / 2.0;

for (int i = 0; i < SLIT_AMOUNT; i++)
{
    double currentOffset = (i - middleIndex) * SLIT_SEGMENT_CENTER_DISTANCE;

    Point3D slitCenter = new Point3D(
        chronotapeFrameOrigin.X + (slitFramesDirection.X * currentOffset),
        chronotapeFrameOrigin.Y + (slitFramesDirection.Y * currentOffset),
        chronotapeFrameOrigin.Z + (slitFramesDirection.Z * currentOffset) + TAPE_TOP_HEIGHT_FROM_GROUND
    );

    Frame newSlit = new Frame(
        slitCenter,
        slitFrameNormal,
        new Vector3D(0, 1, 0),
        SLIT_WIDTH,
        SLIT_HEIGHT
    );

    slits.Add(newSlit);
}

Console.WriteLine("--- Slits ---");
for (int i = 0; i < slits.Count; i++)
{
    Frame slit = slits[i];
    Console.WriteLine($"Slit {i}: X: {slit.Center.X} Y: {slit.Center.Y} Z: {slit.Center.Z}");
}

// --- Ceiling Displayed Segments Setup ---
Vector3D surfaceUp = Vector3D.Cross(displaySurface.Normal, slitFramesDirection);

var displayedSegments = new List<Frame>();

for (int i = 0; i < SLIT_AMOUNT; i++)
{
    double currentOffset = (i - middleIndex) * DISPLAYED_SEGMENT_CENTER_DISTANCE;

    Point3D segmentCenter = new Point3D(
        displaySurface.Point.X + (slitFramesDirection.X * currentOffset),
        displaySurface.Point.Y + (slitFramesDirection.Y * currentOffset),
        displaySurface.Point.Z + (slitFramesDirection.Z * currentOffset)
    );

    Frame newSegment = new Frame(
        segmentCenter,
        displaySurface.Normal,
        surfaceUp,
        DISPLAYED_WIDTH,
        DISPLAYED_HEIGHT
    );

    displayedSegments.Add(newSegment);
}

Console.WriteLine("\n--- Displayed Segments ---");
for (int i = 0; i < displayedSegments.Count; i++)
{
    Frame segment = displayedSegments[i];
    Console.WriteLine($"Display {i}: X: {segment.Center.X} Y: {segment.Center.Y} Z: {segment.Center.Z}");
}

var lightSources = new Point3D?[SLIT_AMOUNT];

Console.WriteLine("\n--- Light Source Positions ---");
for (int i = 0; i < SLIT_AMOUNT; i++)
{
    Frame display = displayedSegments[i];
    Frame slit = slits[i];

    var rays = new List<Ray>
    {
        new Ray(display.TopRight,    new Vector3D(display.TopRight,    slit.TopRight)),
        new Ray(display.TopLeft,     new Vector3D(display.TopLeft,     slit.TopLeft)),
        new Ray(display.BottomRight, new Vector3D(display.BottomRight, slit.BottomRight)),
        new Ray(display.BottomLeft,  new Vector3D(display.BottomLeft,  slit.BottomLeft)),
    };

    if (!GeometryMath.GetClosestPointToRays(rays, out Point3D lightSource))
    {
        Console.WriteLine($"Warning: Could not determine light source for slit {i} — rays may be parallel.");
        continue;
    }

    lightSources[i] = lightSource;
    Console.WriteLine($"Slit {i}: X: {lightSource.X:F2} Y: {lightSource.Y:F2} Z: {lightSource.Z:F2}");
}

if (options is null)
{
    Console.WriteLine("\nNo font projection options passed. Geometry setup only.");
    return;
}

if (options.SlitIndex.HasValue && (options.SlitIndex.Value < 0 || options.SlitIndex.Value >= SLIT_AMOUNT))
{
    Console.WriteLine($"Invalid --slitIndex {options.SlitIndex.Value}. Expected 0..{SLIT_AMOUNT - 1}.");
    return;
}

if (!File.Exists(options.FontPath))
{
    Console.WriteLine($"Font file does not exist: {options.FontPath}");
    return;
}

var sampledPixels = RenderAndSampleText(options);
if (sampledPixels.Count == 0)
{
    Console.WriteLine("No drawable pixels sampled from text.");
    return;
}

var results = new List<SlitProjectionResult>();

for (int i = 0; i < SLIT_AMOUNT; i++)
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

    Plane slitPlane = new Plane(GetFrameNormal(slit), slit.Center);

    Vector3D displayRight = GetFrameRight(display);
    Vector3D displayUp = GetFrameUp(display);
    double displayWidth = GetFrameWidth(display);
    double displayHeight = GetFrameHeight(display);

    Vector3D slitRight = GetFrameRight(slit);
    Vector3D slitUp = GetFrameUp(slit);
    double slitHalfWidth = GetFrameWidth(slit) / 2.0;
    double slitHalfHeight = GetFrameHeight(slit) / 2.0;

    var projectedPoints = new List<ProjectedPoint>();

    foreach (var pixel in sampledPixels)
    {
        double u = (((pixel.X + 0.5) / pixel.BitmapWidth) - 0.5) * displayWidth;
        double v = (0.5 - ((pixel.Y + 0.5) / pixel.BitmapHeight)) * displayHeight;

        Point3D displayPoint = OffsetPoint(display.Center, displayRight, u, displayUp, v);

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

    results.Add(new SlitProjectionResult
    {
        SlitIndex = i,
        LightSource = lightSource,
        SampleCount = projectedPoints.Count,
        Points = projectedPoints
    });

    Console.WriteLine($"Projected slit {i}: {projectedPoints.Count} points.");
}

WriteOutputs(options.OutPath, results, options.SlitIndex.HasValue);

Console.WriteLine("\nDone.");

static ProjectionOptions? ParseProjectionOptions(string[] cliArgs)
{
    if (cliArgs.Length == 0)
    {
        return null;
    }

    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (int i = 0; i < cliArgs.Length; i++)
    {
        string arg = cliArgs[i];
        if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase))
        {
            PrintUsage();
            Environment.Exit(0);
        }

        if (!arg.StartsWith("--", StringComparison.Ordinal))
        {
            Console.WriteLine($"Unknown argument: {arg}");
            PrintUsage();
            Environment.Exit(1);
        }

        if (i + 1 >= cliArgs.Length)
        {
            Console.WriteLine($"Missing value for argument {arg}");
            PrintUsage();
            Environment.Exit(1);
        }

        map[arg] = cliArgs[++i];
    }

    if (!map.TryGetValue("--font", out string? fontPath) || string.IsNullOrWhiteSpace(fontPath))
    {
        Console.WriteLine("--font is required when running projection mode.");
        PrintUsage();
        Environment.Exit(1);
    }

    if (!map.TryGetValue("--text", out string? text) || string.IsNullOrWhiteSpace(text))
    {
        Console.WriteLine("--text is required when running projection mode.");
        PrintUsage();
        Environment.Exit(1);
    }

    var options = new ProjectionOptions
    {
        FontPath = Path.GetFullPath(fontPath),
        Text = text,
        FontSize = ParseDouble(map, "--fontSize", 200),
        SampleStep = ParseInt(map, "--sampleStep", 1),
        OutPath = Path.GetFullPath(map.TryGetValue("--out", out string? outPath) ? outPath : "./projection-out"),
        SampleMode = map.TryGetValue("--sampleMode", out string? sampleMode) ? sampleMode : "fill"
    };

    if (options.SampleStep < 1)
    {
        Console.WriteLine("--sampleStep must be >= 1.");
        Environment.Exit(1);
    }

    if (options.FontSize <= 0)
    {
        Console.WriteLine("--fontSize must be > 0.");
        Environment.Exit(1);
    }

    if (map.TryGetValue("--slitIndex", out string? slitIndexRaw))
    {
        if (!int.TryParse(slitIndexRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int slitIndex))
        {
            Console.WriteLine("--slitIndex must be an integer.");
            Environment.Exit(1);
        }

        options.SlitIndex = slitIndex;
    }

    if (!string.Equals(options.SampleMode, "fill", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(options.SampleMode, "edge", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("--sampleMode must be either 'fill' or 'edge'.");
        Environment.Exit(1);
    }

    return options;
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project ./tape-gen/tape-gen.csproj -- \\");
    Console.WriteLine("    --font /absolute/path/to/font.ttf --text \"12:34\" --fontSize 200 --sampleStep 2 --out ./projection-out [--slitIndex 0] [--sampleMode fill|edge]");
}

static double ParseDouble(Dictionary<string, string> map, string key, double defaultValue)
{
    if (!map.TryGetValue(key, out string? raw))
    {
        return defaultValue;
    }

    if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
    {
        Console.WriteLine($"Invalid numeric value for {key}: {raw}");
        Environment.Exit(1);
    }

    return parsed;
}

static int ParseInt(Dictionary<string, string> map, string key, int defaultValue)
{
    if (!map.TryGetValue(key, out string? raw))
    {
        return defaultValue;
    }

    if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
    {
        Console.WriteLine($"Invalid integer value for {key}: {raw}");
        Environment.Exit(1);
    }

    return parsed;
}

static List<SampledPixel> RenderAndSampleText(ProjectionOptions options)
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
    int width = Math.Max(1, (int)Math.Ceiling(bounds.Width) + padding * 2);
    int height = Math.Max(1, (int)Math.Ceiling(bounds.Height) + padding * 2);

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

static bool IsEdgePixel(SKBitmap bitmap, int x, int y)
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

static Vector3D GetFrameRight(Frame frame)
{
    Vector3D right = new Vector3D(frame.TopLeft, frame.TopRight);
    return Normalize(right);
}

static Vector3D GetFrameUp(Frame frame)
{
    Vector3D up = new Vector3D(frame.BottomLeft, frame.TopLeft);
    return Normalize(up);
}

static Vector3D GetFrameNormal(Frame frame)
{
    Vector3D right = GetFrameRight(frame);
    Vector3D up = GetFrameUp(frame);
    return Normalize(Vector3D.Cross(right, up));
}

static double GetFrameWidth(Frame frame)
{
    return Length(new Vector3D(frame.TopLeft, frame.TopRight));
}

static double GetFrameHeight(Frame frame)
{
    return Length(new Vector3D(frame.BottomLeft, frame.TopLeft));
}

static double Length(Vector3D vector)
{
    return Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
}

static Vector3D Normalize(Vector3D vector)
{
    double len = Length(vector);
    if (len < 1e-12)
    {
        return new Vector3D();
    }

    return new Vector3D(vector.X / len, vector.Y / len, vector.Z / len);
}

static Point3D OffsetPoint(Point3D origin, Vector3D direction1, double amount1, Vector3D direction2, double amount2)
{
    return new Point3D(
        origin.X + direction1.X * amount1 + direction2.X * amount2,
        origin.Y + direction1.Y * amount1 + direction2.Y * amount2,
        origin.Z + direction1.Z * amount1 + direction2.Z * amount2
    );
}

static void WriteOutputs(string outputPath, List<SlitProjectionResult> results, bool singleSlitRequested)
{
    if (results.Count == 0)
    {
        Console.WriteLine("No projected points to write.");
        return;
    }

    string extension = Path.GetExtension(outputPath).ToLowerInvariant();
    bool outputIsSingleFile = extension == ".csv" || extension == ".json";

    if (outputIsSingleFile)
    {
        if (!singleSlitRequested || results.Count != 1)
        {
            throw new InvalidOperationException("When --out is a .csv/.json file, provide --slitIndex to export exactly one slit.");
        }

        if (extension == ".csv")
        {
            WriteCsv(outputPath, results[0]);
        }
        else
        {
            WriteJson(outputPath, results[0]);
        }

        Console.WriteLine($"Wrote {outputPath}");
        return;
    }

    Directory.CreateDirectory(outputPath);

    foreach (var result in results)
    {
        string csvPath = Path.Combine(outputPath, $"slit-{result.SlitIndex}.csv");
        string jsonPath = Path.Combine(outputPath, $"slit-{result.SlitIndex}.json");
        WriteCsv(csvPath, result);
        WriteJson(jsonPath, result);
        Console.WriteLine($"Wrote {csvPath}");
        Console.WriteLine($"Wrote {jsonPath}");
    }
}

static void WriteCsv(string path, SlitProjectionResult result)
{
    using var writer = new StreamWriter(path);
    writer.WriteLine("pixelX,pixelY,displayX,displayY,displayZ,worldX,worldY,worldZ,slitLocalX,slitLocalY");

    foreach (var point in result.Points)
    {
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{point.PixelX},{point.PixelY},{point.DisplayWorldX},{point.DisplayWorldY},{point.DisplayWorldZ},{point.WorldX},{point.WorldY},{point.WorldZ},{point.SlitLocalX},{point.SlitLocalY}"));
    }
}

static void WriteJson(string path, SlitProjectionResult result)
{
    string json = JsonSerializer.Serialize(result, new JsonSerializerOptions
    {
        WriteIndented = true
    });

    File.WriteAllText(path, json);
}

sealed class ProjectionOptions
{
    public string FontPath { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double FontSize { get; set; }
    public int SampleStep { get; set; }
    public string OutPath { get; set; } = string.Empty;
    public int? SlitIndex { get; set; }
    public string SampleMode { get; set; } = "fill";
}

sealed class SampledPixel
{
    public int X { get; set; }
    public int Y { get; set; }
    public int BitmapWidth { get; set; }
    public int BitmapHeight { get; set; }
}

sealed class SlitProjectionResult
{
    public int SlitIndex { get; set; }
    public Point3D LightSource { get; set; }
    public int SampleCount { get; set; }
    public List<ProjectedPoint> Points { get; set; } = new();
}

sealed class ProjectedPoint
{
    public int PixelX { get; set; }
    public int PixelY { get; set; }
    public double DisplayWorldX { get; set; }
    public double DisplayWorldY { get; set; }
    public double DisplayWorldZ { get; set; }
    public double WorldX { get; set; }
    public double WorldY { get; set; }
    public double WorldZ { get; set; }
    public double SlitLocalX { get; set; }
    public double SlitLocalY { get; set; }
}
