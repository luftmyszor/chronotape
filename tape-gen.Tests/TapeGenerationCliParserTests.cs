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
    public void Parse_ConvertsMillimeterValuesWhenDpiProvided()
    {
        string[] args =
        [
            "--generate-tape",
            "--segment-characters", "9876",
            "--main-characters", "6789",
            "--dpi", "600",
            "--segment-width-mm", "25.4",
            "--segment-height-mm", "50.8",
            "--top-margin-mm", "12.7",
            "--main-padding-mm", "0.5",
            "--deadzone-padding-mm", "0.5",
            "--deadzone-left-mm", "2.54",
            "--deadzone-top-mm", "10.16",
            "--deadzone-right-mm", "20.32",
            "--deadzone-bottom-mm", "40.64"
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
        Assert.Equal(60, result.Spec.DeadzoneRectPx.Left);
        Assert.Equal(240, result.Spec.DeadzoneRectPx.Top);
        Assert.Equal(480, result.Spec.DeadzoneRectPx.Right);
        Assert.Equal(960, result.Spec.DeadzoneRectPx.Bottom);
    }

    [Fact]
    public void Parse_PrefersMillimeterValueOverPixelValueWithinSameSource()
    {
        string[] args =
        [
            "--generate-tape",
            "--segment-characters", "9876",
            "--main-characters", "6789",
            "--dpi", "600",
            "--segment-width", "123",
            "--segment-width-mm", "25.4"
        ];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.Null(result.Error);
        Assert.NotNull(result.Spec);
        Assert.Equal(600, result.Spec.SegmentWidthPx);
    }

    [Fact]
    public void Parse_UsesCliMillimetersOverConfigPixels()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "chronotape-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string configPath = Path.Combine(tempDir, "tape-config.json");
        File.WriteAllText(configPath, """
        {
          "SegmentCharacters": "9876",
          "MainCharacters": "6789",
          "SegmentWidthPx": 140,
          "Dpi": 600
        }
        """);

        string[] args =
        [
            "--generate-tape",
            "--tape-config", configPath,
            "--segment-width-mm", "25.4"
        ];

        TapeGenerationParseResult result = TapeGenerationCliParser.Parse(args, _ => null);

        Assert.True(result.ShouldRun);
        Assert.Null(result.Error);
        Assert.NotNull(result.Spec);
        Assert.Equal(600, result.Spec.SegmentWidthPx);
    }

    [Fact]
    public void Parse_FailsWhenMillimeterValueHasNoDpi()
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
        Assert.Contains("requires DPI", result.Error);
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
