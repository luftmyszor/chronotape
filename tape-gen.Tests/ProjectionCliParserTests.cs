using Xunit;

public sealed class ProjectionCliParserTests
{
    [Fact]
    public void Parse_DoesNotRunWithoutProjectionDebugFlag()
    {
        ProjectionParseResult result = ProjectionCliParser.Parse(["--font", "/tmp/font.ttf"]);

        Assert.False(result.ShouldRun);
        Assert.Null(result.Error);
        Assert.Null(result.Options);
    }

    [Fact]
    public void Parse_FailsWhenProjectionDebugMissingFont()
    {
        ProjectionParseResult result = ProjectionCliParser.Parse(["--projection-debug"]);

        Assert.True(result.ShouldRun);
        Assert.NotNull(result.Error);
        Assert.Contains("--font is required", result.Error);
        Assert.Null(result.Options);
    }

    [Fact]
    public void Parse_ParsesProjectionDebugOptions()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "chronotape-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string fontPath = Path.Combine(tempDir, "fake.ttf");
        File.WriteAllBytes(fontPath, [0x00]);
        string worldGeometryPath = Path.Combine(tempDir, "world-geometry.json");
        File.WriteAllText(worldGeometryPath, """
        {
          "SlitWidthMm": 17.78,
          "SlitHeightMm": 17.272,
          "SlitCenterYOffsetMm": 21.844,
          "SlitCount": 4,
          "SlitSegmentCenterDistanceMm": 50.0,
          "TapeTopHeightFromGroundMm": 0.0,
          "DisplayedSegmentWidthMm": 150.0,
          "DisplayedSegmentHeightMm": 300.0,
          "DisplayedSegmentCenterDistanceMm": 160.0,
          "TapeOriginMm": { "XMm": 0.0, "YMm": 0.0, "ZMm": 0.0 },
          "SlitDirection": { "X": 1.0, "Y": 0.0, "Z": 0.0 },
          "SlitNormal": { "X": 0.0, "Y": 0.0, "Z": 1.0 },
          "SlitUpDirection": { "X": 0.0, "Y": 1.0, "Z": 0.0 },
          "DisplayPlanePointMm": { "XMm": 0.0, "YMm": 0.0, "ZMm": 2000.0 },
          "DisplayPlaneNormal": { "X": 0.0, "Y": 0.0, "Z": 1.0 },
          "DisplayPlaneUpDirection": { "X": 0.0, "Y": 1.0, "Z": 0.0 },
          "GlyphPixelSizeMm": 0.1
        }
        """);

        ProjectionParseResult result = ProjectionCliParser.Parse(
        [
            "--projection-debug",
            "--font", fontPath,
            "--world-geometry", worldGeometryPath,
            "--out", "./projection-debug",
            "--text", "9876",
            "--text-size", "240",
            "--sample-step", "3"
        ]);

        Assert.True(result.ShouldRun);
        Assert.Null(result.Error);
        Assert.NotNull(result.Options);
        Assert.Equal(Path.GetFullPath(fontPath), result.Options.FontPath);
        Assert.Equal(Path.GetFullPath("./projection-debug"), result.Options.OutPath);
        Assert.Equal("9876", result.Options.Text);
        Assert.Equal(240, result.Options.TextSize);
        Assert.Equal(3, result.Options.SampleStep);
        Assert.Equal(17.78, result.Options.WorldGeometry.SlitWidthMm);
    }

    [Fact]
    public void Parse_FailsWhenWorldGeometryIsMissing()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "chronotape-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string fontPath = Path.Combine(tempDir, "fake.ttf");
        File.WriteAllBytes(fontPath, [0x00]);

        ProjectionParseResult result = ProjectionCliParser.Parse(
        [
            "--projection-debug",
            "--font", fontPath,
            "--world-geometry", Path.Combine(tempDir, "missing-world-geometry.json")
        ]);

        Assert.True(result.ShouldRun);
        Assert.NotNull(result.Error);
        Assert.Contains("World geometry file does not exist", result.Error);
        Assert.Null(result.Options);
    }
}
