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

        ProjectionParseResult result = ProjectionCliParser.Parse(
        [
            "--projection-debug",
            "--font", fontPath,
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
    }
}
