using Phys;

internal sealed class ProjectionOptions
{
    public string FontPath { get; set; } = string.Empty;
    public string OutPath { get; set; } = string.Empty;
    public string Text { get; set; } = "1234";
    public int TextSize { get; set; } = 200;
    public int SampleStep { get; set; } = 2;
}

internal sealed class ProjectionParseResult
{
    public bool ShouldRun { get; set; }
    public ProjectionOptions? Options { get; set; }
    public string? Error { get; set; }
}

internal sealed class CharacterBitmapSample
{
    public char Character { get; set; }
    public int BitmapWidth { get; set; }
    public int BitmapHeight { get; set; }
    public List<SampledPixel> Pixels { get; set; } = new();
}

internal sealed class SampledPixel
{
    public int X { get; set; }
    public int Y { get; set; }
    public int BitmapWidth { get; set; }
    public int BitmapHeight { get; set; }
}

internal sealed class SlitProjectionResult
{
    public int SlitIndex { get; set; }
    public Point3D LightSource { get; set; }
    public int SampleCount { get; set; }
    public List<ProjectedPoint> Points { get; set; } = new();
}

internal sealed class CharacterSlitBitmap
{
    public char Character { get; set; }
    public bool[][] Bitmap { get; set; } = [];
}

internal sealed class ProjectedPoint
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
