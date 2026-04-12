using System.Text.Json;

internal sealed class WorldGeometryConfig
{
    public double SlitWidthMm { get; set; }
    public double SlitHeightMm { get; set; }
    public int SlitCount { get; set; }
    public double SlitSegmentCenterDistanceMm { get; set; }
    public double TapeTopHeightFromGroundMm { get; set; }
    public double DisplayedSegmentWidthMm { get; set; }
    public double DisplayedSegmentHeightMm { get; set; }
    public double DisplayedSegmentCenterDistanceMm { get; set; }
    public Point3DMmConfig TapeOriginMm { get; set; } = new();
    public Vector3DConfig SlitDirection { get; set; } = new();
    public Vector3DConfig SlitNormal { get; set; } = new();
    public Vector3DConfig SlitUpDirection { get; set; } = new();
    public Point3DMmConfig DisplayPlanePointMm { get; set; } = new();
    public Vector3DConfig DisplayPlaneNormal { get; set; } = new();
    public Vector3DConfig DisplayPlaneUpDirection { get; set; } = new();
    public double GlyphPixelSizeMm { get; set; }
}

internal sealed class Point3DMmConfig
{
    public double XMm { get; set; }
    public double YMm { get; set; }
    public double ZMm { get; set; }
}

internal sealed class Vector3DConfig
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

internal static class WorldGeometryConfigLoader
{
    public static string ResolveDefaultPath()
    {
        string currentDirTapeGen = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "tape-gen", "world-geometry.json"));
        if (File.Exists(currentDirTapeGen))
        {
            return currentDirTapeGen;
        }

        string currentDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "world-geometry.json"));
        if (File.Exists(currentDir))
        {
            return currentDir;
        }

        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "tape-gen", "world-geometry.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return currentDirTapeGen;
    }

    public static bool TryLoad(string configPath, out WorldGeometryConfig? config, out string? error)
    {
        config = null;
        error = null;
        string fullPath = Path.GetFullPath(configPath);
        if (!File.Exists(fullPath))
        {
            error = $"World geometry file does not exist: {fullPath}";
            return false;
        }

        try
        {
            string configJson = File.ReadAllText(fullPath);
            config = JsonSerializer.Deserialize<WorldGeometryConfig>(configJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            error = $"Failed to read world geometry config '{fullPath}': {ex.Message}";
            return false;
        }

        if (config is null)
        {
            error = $"Failed to parse world geometry config '{fullPath}'.";
            return false;
        }

        if (!ValidatePositiveMm(config.SlitWidthMm, nameof(config.SlitWidthMm), out error)
            || !ValidatePositiveMm(config.SlitHeightMm, nameof(config.SlitHeightMm), out error)
            || !ValidatePositiveInt(config.SlitCount, nameof(config.SlitCount), out error)
            || !ValidatePositiveMm(config.SlitSegmentCenterDistanceMm, nameof(config.SlitSegmentCenterDistanceMm), out error)
            || !ValidateFiniteMm(config.TapeTopHeightFromGroundMm, nameof(config.TapeTopHeightFromGroundMm), out error)
            || !ValidatePositiveMm(config.DisplayedSegmentWidthMm, nameof(config.DisplayedSegmentWidthMm), out error)
            || !ValidatePositiveMm(config.DisplayedSegmentHeightMm, nameof(config.DisplayedSegmentHeightMm), out error)
            || !ValidatePositiveMm(config.DisplayedSegmentCenterDistanceMm, nameof(config.DisplayedSegmentCenterDistanceMm), out error)
            || !ValidatePoint(config.TapeOriginMm, nameof(config.TapeOriginMm), out error)
            || !ValidatePoint(config.DisplayPlanePointMm, nameof(config.DisplayPlanePointMm), out error)
            || !ValidateVector(config.SlitDirection, nameof(config.SlitDirection), out error)
            || !ValidateVector(config.SlitNormal, nameof(config.SlitNormal), out error)
            || !ValidateVector(config.SlitUpDirection, nameof(config.SlitUpDirection), out error)
            || !ValidateVector(config.DisplayPlaneNormal, nameof(config.DisplayPlaneNormal), out error)
            || !ValidateVector(config.DisplayPlaneUpDirection, nameof(config.DisplayPlaneUpDirection), out error)
            || !ValidatePositiveMm(config.GlyphPixelSizeMm, nameof(config.GlyphPixelSizeMm), out error))
        {
            config = null;
            return false;
        }

        return true;
    }

    private static bool ValidatePositiveMm(double value, string name, out string? error)
    {
        if (!double.IsFinite(value) || value <= 0d)
        {
            error = $"World geometry field '{name}' must be a finite value > 0 mm.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool ValidateFiniteMm(double value, string name, out string? error)
    {
        if (!double.IsFinite(value))
        {
            error = $"World geometry field '{name}' must be a finite millimeter value.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool ValidatePositiveInt(int value, string name, out string? error)
    {
        if (value <= 0)
        {
            error = $"World geometry field '{name}' must be > 0.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool ValidatePoint(Point3DMmConfig point, string name, out string? error)
    {
        if (!double.IsFinite(point.XMm) || !double.IsFinite(point.YMm) || !double.IsFinite(point.ZMm))
        {
            error = $"World geometry field '{name}' must contain finite XMm/YMm/ZMm values.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool ValidateVector(Vector3DConfig vector, string name, out string? error)
    {
        if (!double.IsFinite(vector.X) || !double.IsFinite(vector.Y) || !double.IsFinite(vector.Z))
        {
            error = $"World geometry field '{name}' must contain finite X/Y/Z values.";
            return false;
        }

        if (vector.X == 0d && vector.Y == 0d && vector.Z == 0d)
        {
            error = $"World geometry field '{name}' must not be a zero vector.";
            return false;
        }

        error = null;
        return true;
    }
}
