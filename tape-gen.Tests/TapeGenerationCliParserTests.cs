using Xunit;

public sealed class TapeGenerationCliParserTests
{
    [Fact]
    public void Parse_UsesActualValuesProvidedByCli()
    {
        string tempDir = CreateTempDir();
        string worldGeometryPath = WriteWorldGeometry(tempDir, slitCount: 2);

        string[] args =
        [
            "--generate-tape",
            "--segment-characters", "9876",
            "--main-characters", "6789",
            "--offset", "1",
            "--slit-count", "2",
            "--world-geometry", worldGeometryPath,
            "--tape-out", "./actual-tape.png"
        ];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.Null(result.Error);
        Assert.NotNull(result.Spec);
        Assert.Equal("9876", result.Spec.SegmentCharacters);
        Assert.Equal("6789", result.Spec.MainCharacters);
        Assert.Equal(1, result.Spec.Offset);
        Assert.Equal(2, result.Spec.SlitCount);
        Assert.Equal("./actual-tape.png", result.Spec.OutputPath);
    }

    [Fact]
    public void Parse_FailsWhenRequiredValuesMissing()
    {
        string[] args = ["--generate-tape"];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.NotNull(result.Error);
        Assert.Contains("Missing required values", result.Error);
        Assert.Null(result.Spec);
    }

    [Fact]
    public void Parse_UsesFontPathFromCliWhenProvided()
    {
        string tempDir = CreateTempDir();
        string worldGeometryPath = WriteWorldGeometry(tempDir);
        string fontPath = Path.Combine(tempDir, "fake.ttf");
        File.WriteAllBytes(fontPath, [0x00]);

        string[] args =
        [
            "--generate-tape",
            "--segment-characters", "9876",
            "--main-characters", "6789",
            "--world-geometry", worldGeometryPath,
            "--font", fontPath
        ];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.Null(result.Error);
        Assert.NotNull(result.Spec);
        Assert.Equal(Path.GetFullPath(fontPath), result.Spec.FontPath);
    }

    [Fact]
    public void Parse_FailsWhenConfiguredFontPathDoesNotExist()
    {
        string tempDir = CreateTempDir();
        string worldGeometryPath = WriteWorldGeometry(tempDir);

        string[] args =
        [
            "--generate-tape",
            "--segment-characters", "9876",
            "--main-characters", "6789",
            "--world-geometry", worldGeometryPath,
            "--font", "/definitely/missing/font.ttf"
        ];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.NotNull(result.Error);
        Assert.Contains("Font file does not exist", result.Error);
        Assert.Null(result.Spec);
    }

    [Fact]
    public void Parse_UsesMillimeterGeometryFromConfigAndWorldGeometry()
    {
        string tempDir = CreateTempDir();
        string configPath = Path.Combine(tempDir, "tape-config.json");
        File.WriteAllText(configPath, """
        {
          "SegmentCharacters": "9876",
          "MainCharacters": "6789",
          "Dpi": 600,
          "SegmentWidthMm": 25.4,
          "SegmentHeightMm": 50.8,
          "TopMarginMm": 12.7,
          "MainHorizontalPaddingMm": 0.5,
          "MainVerticalPaddingMm": 0.75,
          "SlitCenterYOffsetMm": 7.62
        }
        """);
        string worldGeometryPath = WriteWorldGeometry(tempDir, slitWidthMm: 17.78, slitHeightMm: 30.48);

        string[] args =
        [
            "--generate-tape",
            "--tape-config", configPath,
            "--world-geometry", worldGeometryPath
        ];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.Null(result.Error);
        Assert.NotNull(result.Spec);
        Assert.Equal(600, result.Spec.SegmentWidthPx);
        Assert.Equal(1200, result.Spec.SegmentHeightPx);
        Assert.Equal(300, result.Spec.TopMarginPx);
        Assert.Equal(12, result.Spec.MainPaddingXPx);
        Assert.Equal(18, result.Spec.MainPaddingYPx);
        Assert.Equal(420, result.Spec.SlitWidthPx);
        Assert.Equal(720, result.Spec.SlitHeightPx);
        Assert.Equal(180, result.Spec.SlitCenterYOffsetPx);
    }

    [Fact]
    public void Parse_IgnoresLegacyDeadzonePaddingFieldsInConfig()
    {
        string tempDir = CreateTempDir();
        string configPath = Path.Combine(tempDir, "tape-config.json");
        File.WriteAllText(configPath, """
        {
          "SegmentCharacters": "9876",
          "MainCharacters": "6789",
          "Dpi": 600,
          "MainHorizontalPaddingMm": 0.5,
          "MainVerticalPaddingMm": 0.75,
          "DeadzoneHorizontalPaddingMm": 50,
          "DeadzoneVerticalPaddingMm": 50,
          "DeadzonePaddingMm": 50
        }
        """);
        string worldGeometryPath = WriteWorldGeometry(tempDir);

        string[] args =
        [
            "--generate-tape",
            "--tape-config", configPath,
            "--world-geometry", worldGeometryPath
        ];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.Null(result.Error);
        Assert.NotNull(result.Spec);
        Assert.Equal(12, result.Spec.MainPaddingXPx);
        Assert.Equal(18, result.Spec.MainPaddingYPx);
    }

    [Fact]
    public void Parse_FailsOnGeometryCliFlags()
    {
        string[] args =
        [
            "--generate-tape",
            "--segment-characters", "9876",
            "--main-characters", "6789",
            "--segment-width-mm", "25.4"
        ];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.NotNull(result.Error);
        Assert.Contains("Unknown argument for tape generation", result.Error);
        Assert.Null(result.Spec);
    }

    [Fact]
    public void Parse_UsesBuiltInMillimeterDefaultsWhenConfigOmitted()
    {
        string tempDir = CreateTempDir();
        string worldGeometryPath = WriteWorldGeometry(tempDir, slitWidthMm: 9.144, slitHeightMm: 9.144);

        string[] args =
        [
            "--generate-tape",
            "--segment-characters", "9876",
            "--main-characters", "6789",
            "--world-geometry", worldGeometryPath
        ];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.Null(result.Error);
        Assert.NotNull(result.Spec);
        Assert.Equal(140, result.Spec.SegmentWidthPx);
        Assert.Equal(210, result.Spec.SegmentHeightPx);
        Assert.Equal(30, result.Spec.TopMarginPx);
        Assert.Equal(8, result.Spec.MainPaddingXPx);
        Assert.Equal(8, result.Spec.MainPaddingYPx);
        Assert.Equal(36, result.Spec.SlitWidthPx);
        Assert.Equal(36, result.Spec.SlitHeightPx);
        Assert.Equal(86, result.Spec.SlitCenterYOffsetPx);
    }

    [Fact]
    public void Parse_FailsWhenConfigDpiIsInvalid()
    {
        string tempDir = CreateTempDir();
        string configPath = Path.Combine(tempDir, "tape-config.json");
        File.WriteAllText(configPath, """
        {
          "SegmentCharacters": "9876",
          "MainCharacters": "6789",
          "Dpi": 0
        }
        """);
        string worldGeometryPath = WriteWorldGeometry(tempDir);

        string[] args =
        [
            "--generate-tape",
            "--tape-config", configPath,
            "--world-geometry", worldGeometryPath
        ];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.NotNull(result.Error);
        Assert.Contains("Dpi in --tape-config must be > 0.", result.Error);
        Assert.Null(result.Spec);
    }

    [Fact]
    public void Parse_SetsHighlightRectFlag()
    {
        string tempDir = CreateTempDir();
        string worldGeometryPath = WriteWorldGeometry(tempDir);

        string[] args =
        [
            "--generate-tape",
            "--segment-characters", "9876",
            "--main-characters", "6789",
            "--world-geometry", worldGeometryPath,
            "--highlight-rects"
        ];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.Null(result.Error);
        Assert.NotNull(result.Spec);
        Assert.True(result.Spec.DebugHighlightRects);
    }

    [Fact]
    public void Parse_FailsWhenSlitCountDiffersFromWorldGeometry()
    {
        string tempDir = CreateTempDir();
        string worldGeometryPath = WriteWorldGeometry(tempDir, slitCount: 4);

        string[] args =
        [
            "--generate-tape",
            "--segment-characters", "9876",
            "--main-characters", "6789",
            "--world-geometry", worldGeometryPath,
            "--slit-count", "2"
        ];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.NotNull(result.Error);
        Assert.Contains("must match world geometry", result.Error);
        Assert.Null(result.Spec);
    }

    [Fact]
    public void Parse_FailsWhenWorldGeometryFieldIsMissingOrInvalid()
    {
        string tempDir = CreateTempDir();
        string worldGeometryPath = Path.Combine(tempDir, "world-geometry.json");
        File.WriteAllText(worldGeometryPath, """
        {
          "SlitWidthMm": 0,
          "SlitHeightMm": 10,
          "SlitCount": 4,
          "SlitSegmentCenterDistanceMm": 50,
          "TapeTopHeightFromGroundMm": 0,
          "DisplayedSegmentWidthMm": 150,
          "DisplayedSegmentHeightMm": 300,
          "DisplayedSegmentCenterDistanceMm": 160,
          "TapeOriginMm": { "XMm": 0, "YMm": 0, "ZMm": 0 },
          "SlitDirection": { "X": 1, "Y": 0, "Z": 0 },
          "SlitNormal": { "X": 0, "Y": 0, "Z": 1 },
          "SlitUpDirection": { "X": 0, "Y": 1, "Z": 0 },
          "DisplayPlanePointMm": { "XMm": 0, "YMm": 0, "ZMm": 2000 },
          "DisplayPlaneNormal": { "X": 0, "Y": 0, "Z": 1 },
          "DisplayPlaneUpDirection": { "X": 0, "Y": 1, "Z": 0 }
        }
        """);

        string[] args =
        [
            "--generate-tape",
            "--segment-characters", "9876",
            "--main-characters", "6789",
            "--world-geometry", worldGeometryPath
        ];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.NotNull(result.Error);
        Assert.Contains("SlitWidthMm", result.Error);
        Assert.Null(result.Spec);
    }

    private static string CreateTempDir()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "chronotape-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private static string WriteWorldGeometry(
        string directory,
        int slitCount = 4,
        double slitWidthMm = 17.78,
        double slitHeightMm = 17.272)
    {
        string worldGeometryPath = Path.Combine(directory, "world-geometry.json");
        File.WriteAllText(worldGeometryPath, $$"""
        {
          "SlitWidthMm": {{slitWidthMm}},
          "SlitHeightMm": {{slitHeightMm}},
          "SlitCount": {{slitCount}},
          "SlitSegmentCenterDistanceMm": 50.0,
          "TapeTopHeightFromGroundMm": 0.0,
          "DisplayedSegmentWidthMm": 150.0,
          "DisplayedSegmentHeightMm": 300.0,
          "DisplayedSegmentCenterDistanceMm": 160.0,
          "TapeOriginMm": {
            "XMm": 0.0,
            "YMm": 0.0,
            "ZMm": 0.0
          },
          "SlitDirection": {
            "X": 1.0,
            "Y": 0.0,
            "Z": 0.0
          },
          "SlitNormal": {
            "X": 0.0,
            "Y": 0.0,
            "Z": 1.0
          },
          "SlitUpDirection": {
            "X": 0.0,
            "Y": 1.0,
            "Z": 0.0
          },
          "DisplayPlanePointMm": {
            "XMm": 0.0,
            "YMm": 0.0,
            "ZMm": 2000.0
          },
          "DisplayPlaneNormal": {
            "X": 0.0,
            "Y": 0.0,
            "Z": 1.0
          },
          "DisplayPlaneUpDirection": {
            "X": 0.0,
            "Y": 1.0,
            "Z": 0.0
          },
          "GlyphPixelSizeMm": 0.1
        }
        """);

        return worldGeometryPath;
    }
}
