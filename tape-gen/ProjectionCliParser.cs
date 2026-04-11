using System.Globalization;

internal static class ProjectionCliParser
{
    private const string ProjectionDebugFlag = "--projection-debug";
    private static readonly HashSet<string> SupportedArguments =
    [
        ProjectionDebugFlag,
        "--font",
        "--out",
        "--text",
        "--text-size",
        "--sample-step"
    ];

    public static ProjectionParseResult Parse(string[] cliArgs)
    {
        bool hasProjectionDebug = cliArgs.Contains(ProjectionDebugFlag, StringComparer.OrdinalIgnoreCase);
        if (!hasProjectionDebug)
        {
            return new ProjectionParseResult { ShouldRun = false };
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < cliArgs.Length; i++)
        {
            string arg = cliArgs[i];
            if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (arg.Equals(ProjectionDebugFlag, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                return ErrorResult($"Unknown argument: {arg}");
            }

            if (!SupportedArguments.Contains(arg))
            {
                return ErrorResult($"Unknown argument for projection debug mode: {arg}");
            }

            if (i + 1 >= cliArgs.Length)
            {
                return ErrorResult($"Missing value for argument {arg}");
            }

            map[arg] = cliArgs[++i];
        }

        if (!map.TryGetValue("--font", out string? fontPath) || string.IsNullOrWhiteSpace(fontPath))
        {
            return ErrorResult("--font is required when running --projection-debug.");
        }

        string fullFontPath = Path.GetFullPath(fontPath);
        if (!File.Exists(fullFontPath))
        {
            return ErrorResult($"Font file does not exist: {fullFontPath}");
        }

        if (!TryParseInt(map, "--text-size", 200, out int textSize, out string? textSizeError))
        {
            return ErrorResult(textSizeError!);
        }

        if (!TryParseInt(map, "--sample-step", 2, out int sampleStep, out string? sampleStepError))
        {
            return ErrorResult(sampleStepError!);
        }

        var options = new ProjectionOptions
        {
            FontPath = fullFontPath,
            OutPath = Path.GetFullPath(map.TryGetValue("--out", out string? outPath) ? outPath : "./projection-out"),
            Text = map.TryGetValue("--text", out string? text) && !string.IsNullOrWhiteSpace(text) ? text : "1234",
            TextSize = textSize,
            SampleStep = sampleStep
        };

        return new ProjectionParseResult
        {
            ShouldRun = true,
            Options = options
        };
    }

    internal static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project ./tape-gen/tape-gen.csproj -- --generate-tape --segment-characters <chars> --main-characters <chars> [--font /path/to/font.ttf] [--tape-out ./tape.png]");
        Console.WriteLine();
        Console.WriteLine("Tape generation value sources (precedence):");
        Console.WriteLine("  CLI flags > environment variables > --tape-config JSON > defaults");
        Console.WriteLine("  Required values: SegmentCharacters, MainCharacters");
        Console.WriteLine("  Env vars: CHRONOTAPE_SEGMENT_CHARACTERS, CHRONOTAPE_MAIN_CHARACTERS, CHRONOTAPE_FONT_PATH, CHRONOTAPE_OUTPUT_PATH, ...");
        Console.WriteLine("  Glyph mode: legacy renderer by default, font-driven mode when --font/FontPath is provided");
        Console.WriteLine();
        Console.WriteLine("Sample tape (documentation/testing):");
        Console.WriteLine("  dotnet run --project ./tape-gen/tape-gen.csproj -- --sample-tape [--sample-out ./tape.png]");
        Console.WriteLine();
        Console.WriteLine("Projection debug mode:");
        Console.WriteLine("  dotnet run --project ./tape-gen/tape-gen.csproj -- \\");
        Console.WriteLine("    --projection-debug --font /absolute/path/to/font.ttf [--out ./projection-out]");
    }

    private static bool TryParseInt(
        IReadOnlyDictionary<string, string> map,
        string argumentName,
        int defaultValue,
        out int value,
        out string? error)
    {
        if (!map.TryGetValue(argumentName, out string? raw))
        {
            value = defaultValue;
            error = null;
            return true;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            error = $"Invalid integer for {argumentName}: '{raw}'.";
            return false;
        }

        if (value <= 0)
        {
            error = $"{argumentName} must be > 0.";
            return false;
        }

        error = null;
        return true;
    }

    private static ProjectionParseResult ErrorResult(string message) => new()
    {
        ShouldRun = true,
        Error = message
    };
}
