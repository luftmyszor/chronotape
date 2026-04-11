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
}
