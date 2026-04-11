using SkiaSharp;

TapeGenerationParseResult tapeParse = TapeGenerationCliParser.Parse(args);
if (tapeParse.ShouldRun)
{
    if (tapeParse.Error is not null || tapeParse.Spec is null)
    {
        Console.WriteLine(tapeParse.Error ?? "Failed to parse tape generation options.");
        ProjectionCliParser.PrintUsage();
        Environment.Exit(1);
    }

    try
    {
        TapeBitmapGenerator.ExportTape(tapeParse.Spec);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to generate tape: {ex.Message}");
        Environment.Exit(1);
    }

    Console.WriteLine($"Generated tape(s) at: {Path.GetFullPath(tapeParse.Spec.OutputPath)}");
    return;
}

if (TryRunTapeSample(args))
{
    return;
}

ProjectionParseResult projectionParse = ProjectionCliParser.Parse(args);
if (projectionParse.ShouldRun)
{
    if (projectionParse.Error is not null || projectionParse.Options is null)
    {
        Console.WriteLine(projectionParse.Error ?? "Failed to parse projection debug options.");
        ProjectionCliParser.PrintUsage();
        Environment.Exit(1);
    }

    try
    {
        ProjectionDebugRunner.Run(projectionParse.Options);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to run projection debug: {ex.Message}");
        Environment.Exit(1);
    }

    Console.WriteLine($"Projection debug artifacts written to: {projectionParse.Options.OutPath}");
    return;
}

ProjectionCliParser.PrintUsage();

bool TryRunTapeSample(string[] cliArgs)
{
    if (!cliArgs.Contains("--sample-tape", StringComparer.OrdinalIgnoreCase))
    {
        return false;
    }

    string outputPath = "./tape.png";
    for (int i = 0; i < cliArgs.Length - 1; i++)
    {
        if (string.Equals(cliArgs[i], "--sample-out", StringComparison.OrdinalIgnoreCase))
        {
            outputPath = cliArgs[i + 1];
            break;
        }
    }

    var sample = new TapeSpec
    {
        SegmentCharacters = "1234",
        MainCharacters = "1234",
        Offset = 2,
        SlitCount = 4,
        SegmentWidthPx = 140,
        SegmentHeightPx = 210,
        TopMarginPx = 30,
        SlitWidthPx = 36,
        SlitHeightPx = 36,
        SlitCenterYOffsetPx = 61,
        FontFamily = "monospace",
        FontStyle = SKFontStyle.Normal,
        ForegroundColor = SKColors.White,
        BackgroundColor = SKColors.Black,
        MainPaddingXPx = 8,
        MainPaddingYPx = 8,
        DeadzonePaddingXPx = 2,
        DeadzonePaddingYPx = 2,
        OutputPath = outputPath
    };

    TapeBitmapGenerator.ExportTape(sample);
    Console.WriteLine($"Generated sample tape(s) at: {Path.GetFullPath(outputPath)}");
    return true;
}
