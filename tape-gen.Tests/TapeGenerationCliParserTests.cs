using Xunit;

public sealed class TapeGenerationCliParserTests
{
    [Fact]
    public void Parse_UsesActualValuesProvidedByCli()
    {
        string[] args =
        [
            "--generate-tape",
            "--segment-characters", "9876",
            "--main-characters", "6789",
            "--offset", "1",
            "--slit-count", "2",
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
        string tempDir = Path.Combine(Path.GetTempPath(), "chronotape-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string fontPath = Path.Combine(tempDir, "fake.ttf");
        File.WriteAllBytes(fontPath, [0x00]);

        string[] args =
        [
            "--generate-tape",
            "--segment-characters", "9876",
            "--main-characters", "6789",
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
        string[] args =
        [
            "--generate-tape",
            "--segment-characters", "9876",
            "--main-characters", "6789",
            "--font", "/definitely/missing/font.ttf"
        ];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.NotNull(result.Error);
        Assert.Contains("Font file does not exist", result.Error);
        Assert.Null(result.Spec);
    }

    [Fact]
    public void Parse_UsesMillimeterGeometryFromConfig()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "chronotape-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string configPath = Path.Combine(tempDir, "tape-config.json");
        File.WriteAllText(configPath, """
        {
          "SegmentCharacters": "9876",
          "MainCharacters": "6789",
          "Dpi": 600,
          "SegmentWidthMm": 25.4,
          "SegmentHeightMm": 50.8,
          "TopMarginMm": 12.7,
          "MainPaddingMm": 0.5,
          "DeadzonePaddingMm": 0.5,
          "SlitWidthMm": 17.78,
          "SlitHeightMm": 30.48,
          "SlitCenterYOffsetMm": 7.62
        }
        """);

        string[] args =
        [
            "--generate-tape",
            "--tape-config", configPath
        ];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.Null(result.Error);
        Assert.NotNull(result.Spec);
        Assert.Equal(600, result.Spec.SegmentWidthPx);
        Assert.Equal(1200, result.Spec.SegmentHeightPx);
        Assert.Equal(300, result.Spec.TopMarginPx);
        Assert.Equal(12, result.Spec.MainPaddingPx);
        Assert.Equal(12, result.Spec.DeadzonePaddingPx);
        Assert.Equal(420, result.Spec.SlitWidthPx);
        Assert.Equal(720, result.Spec.SlitHeightPx);
        Assert.Equal(180, result.Spec.SlitCenterYOffsetPx);
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
        string[] args =
        [
            "--generate-tape",
            "--segment-characters", "9876",
            "--main-characters", "6789"
        ];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.Null(result.Error);
        Assert.NotNull(result.Spec);
        Assert.Equal(140, result.Spec.SegmentWidthPx);
        Assert.Equal(210, result.Spec.SegmentHeightPx);
        Assert.Equal(30, result.Spec.TopMarginPx);
        Assert.Equal(8, result.Spec.MainPaddingPx);
        Assert.Equal(2, result.Spec.DeadzonePaddingPx);
        Assert.Equal(36, result.Spec.SlitWidthPx);
        Assert.Equal(36, result.Spec.SlitHeightPx);
        Assert.Equal(61, result.Spec.SlitCenterYOffsetPx);
    }

    [Fact]
    public void Parse_DerivesSlitGeometryFromLegacyDeadzoneRectWhenNeeded()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "chronotape-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string configPath = Path.Combine(tempDir, "tape-config.json");
        File.WriteAllText(configPath, """
        {
          "SegmentCharacters": "9876",
          "MainCharacters": "6789",
          "Dpi": 600,
          "SegmentHeightMm": 50.8,
          "DeadzoneRectMm": {
            "Left": 2.54,
            "Top": 10.16,
            "Right": 20.32,
            "Bottom": 40.64
          }
        }
        """);

        string[] args =
        [
            "--generate-tape",
            "--tape-config", configPath
        ];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.Null(result.Error);
        Assert.NotNull(result.Spec);
        Assert.Equal(420, result.Spec.SlitWidthPx);
        Assert.Equal(720, result.Spec.SlitHeightPx);
        Assert.Equal(0, result.Spec.SlitCenterYOffsetPx);
    }

    [Fact]
    public void Parse_PrefersExplicitSlitFieldsOverLegacyDeadzoneRect()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "chronotape-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string configPath = Path.Combine(tempDir, "tape-config.json");
        File.WriteAllText(configPath, """
        {
          "SegmentCharacters": "9876",
          "MainCharacters": "6789",
          "Dpi": 600,
          "SlitWidthMm": 10.16,
          "SlitHeightMm": 12.7,
          "SlitCenterYOffsetMm": 5.08,
          "DeadzoneRectMm": {
            "Left": 2.54,
            "Top": 10.16,
            "Right": 20.32,
            "Bottom": 40.64
          }
        }
        """);

        string[] args =
        [
            "--generate-tape",
            "--tape-config", configPath
        ];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.Null(result.Error);
        Assert.NotNull(result.Spec);
        Assert.Equal(240, result.Spec.SlitWidthPx);
        Assert.Equal(300, result.Spec.SlitHeightPx);
        Assert.Equal(120, result.Spec.SlitCenterYOffsetPx);
    }

    [Fact]
    public void Parse_FailsWhenConfigDpiIsInvalid()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "chronotape-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string configPath = Path.Combine(tempDir, "tape-config.json");
        File.WriteAllText(configPath, """
        {
          "SegmentCharacters": "9876",
          "MainCharacters": "6789",
          "Dpi": 0
        }
        """);

        string[] args =
        [
            "--generate-tape",
            "--tape-config", configPath
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
        string[] args =
        [
            "--generate-tape",
            "--segment-characters", "9876",
            "--main-characters", "6789",
            "--highlight-rects"
        ];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.Null(result.Error);
        Assert.NotNull(result.Spec);
        Assert.True(result.Spec.DebugHighlightRects);
    }
}
