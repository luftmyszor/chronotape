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
}
