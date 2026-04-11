using System.Globalization;
using System.Text.Json;
using SkiaSharp;

internal sealed class TapeGenerationParseResult
{
    public bool ShouldRun { get; set; }
    public TapeSpec? Spec { get; set; }
    public string? Error { get; set; }
}

internal static class TapeGenerationCliParser
{
    private const string GenerateFlag = "--generate-tape";
    private const string SampleFlag = "--sample-tape";
    private static readonly HashSet<string> SupportedArguments =
    [
        GenerateFlag,
        "--tape-config",
        "--tape-out",
        "--segment-characters",
        "--main-characters",
        "--offset",
        "--slit-count",
        "--segment-width",
        "--segment-height",
        "--top-margin",
        "--deadzone-left",
        "--deadzone-top",
        "--deadzone-right",
        "--deadzone-bottom",
        "--main-padding",
        "--deadzone-padding",
        "--font",
        "--font-family",
        "--debug-rects"
    ];

    public static TapeGenerationParseResult Parse(string[] cliArgs, Func<string, string?>? environmentReader = null)
    {
        bool hasGenerate = cliArgs.Contains(GenerateFlag, StringComparer.OrdinalIgnoreCase);
        bool hasSample = cliArgs.Contains(SampleFlag, StringComparer.OrdinalIgnoreCase);
        if (!hasGenerate)
        {
            return new TapeGenerationParseResult { ShouldRun = false };
        }

        if (hasSample)
        {
            return ErrorResult("Use either --generate-tape or --sample-tape, not both.");
        }

        environmentReader ??= Environment.GetEnvironmentVariable;

        if (!TryParseArguments(cliArgs, out Dictionary<string, string> argsMap, out string? argsError))
        {
            return ErrorResult(argsError!);
        }

        TapeConfigFile config = new();
        if (argsMap.TryGetValue("--tape-config", out string? configPath))
        {
            string fullConfigPath = Path.GetFullPath(configPath);
            if (!File.Exists(fullConfigPath))
            {
                return ErrorResult($"Tape config file does not exist: {fullConfigPath}");
            }

            try
            {
                string configJson = File.ReadAllText(fullConfigPath);
                config = JsonSerializer.Deserialize<TapeConfigFile>(configJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new TapeConfigFile();
            }
            catch (Exception ex)
            {
                return ErrorResult($"Failed to read tape config '{fullConfigPath}': {ex.Message}");
            }
        }

        string? segmentCharacters = FirstNonEmpty(
            GetArg(argsMap, "--segment-characters"),
            environmentReader("CHRONOTAPE_SEGMENT_CHARACTERS"),
            config.SegmentCharacters);

        string? mainCharacters = FirstNonEmpty(
            GetArg(argsMap, "--main-characters"),
            environmentReader("CHRONOTAPE_MAIN_CHARACTERS"),
            config.MainCharacters);

        if (string.IsNullOrWhiteSpace(segmentCharacters) || string.IsNullOrWhiteSpace(mainCharacters))
        {
            return ErrorResult(
                "Missing required values. Provide both SegmentCharacters and MainCharacters via " +
                "--segment-characters/--main-characters, CHRONOTAPE_SEGMENT_CHARACTERS/CHRONOTAPE_MAIN_CHARACTERS, or --tape-config.");
        }

        if (!TryResolveInt(argsMap, environmentReader, "--offset", "CHRONOTAPE_OFFSET", config.Offset, defaultValue: 0, out int offset, out string? offsetError))
        {
            return ErrorResult(offsetError!);
        }

        if (!TryResolveInt(argsMap, environmentReader, "--slit-count", "CHRONOTAPE_SLIT_COUNT", config.SlitCount, defaultValue: 1, out int slitCount, out string? slitCountError))
        {
            return ErrorResult(slitCountError!);
        }

        if (!TryResolveInt(argsMap, environmentReader, "--segment-width", "CHRONOTAPE_SEGMENT_WIDTH", config.SegmentWidthPx, defaultValue: 140, out int segmentWidthPx, out string? segmentWidthError))
        {
            return ErrorResult(segmentWidthError!);
        }

        if (!TryResolveInt(argsMap, environmentReader, "--segment-height", "CHRONOTAPE_SEGMENT_HEIGHT", config.SegmentHeightPx, defaultValue: 210, out int segmentHeightPx, out string? segmentHeightError))
        {
            return ErrorResult(segmentHeightError!);
        }

        if (!TryResolveInt(argsMap, environmentReader, "--top-margin", "CHRONOTAPE_TOP_MARGIN", config.TopMarginPx, defaultValue: 30, out int topMarginPx, out string? topMarginError))
        {
            return ErrorResult(topMarginError!);
        }

        if (!TryResolveInt(argsMap, environmentReader, "--main-padding", "CHRONOTAPE_MAIN_PADDING", config.MainPaddingPx, defaultValue: 8, out int mainPaddingPx, out string? mainPaddingError))
        {
            return ErrorResult(mainPaddingError!);
        }

        if (!TryResolveInt(argsMap, environmentReader, "--deadzone-padding", "CHRONOTAPE_DEADZONE_PADDING", config.DeadzonePaddingPx, defaultValue: 2, out int deadzonePaddingPx, out string? deadzonePaddingError))
        {
            return ErrorResult(deadzonePaddingError!);
        }

        if (!TryResolveInt(argsMap, environmentReader, "--deadzone-left", "CHRONOTAPE_DEADZONE_LEFT", config.DeadzoneRectPx?.Left, defaultValue: 52, out int deadzoneLeft, out string? deadzoneLeftError))
        {
            return ErrorResult(deadzoneLeftError!);
        }

        if (!TryResolveInt(argsMap, environmentReader, "--deadzone-top", "CHRONOTAPE_DEADZONE_TOP", config.DeadzoneRectPx?.Top, defaultValue: 148, out int deadzoneTop, out string? deadzoneTopError))
        {
            return ErrorResult(deadzoneTopError!);
        }

        if (!TryResolveInt(argsMap, environmentReader, "--deadzone-right", "CHRONOTAPE_DEADZONE_RIGHT", config.DeadzoneRectPx?.Right, defaultValue: 88, out int deadzoneRight, out string? deadzoneRightError))
        {
            return ErrorResult(deadzoneRightError!);
        }

        if (!TryResolveInt(argsMap, environmentReader, "--deadzone-bottom", "CHRONOTAPE_DEADZONE_BOTTOM", config.DeadzoneRectPx?.Bottom, defaultValue: 184, out int deadzoneBottom, out string? deadzoneBottomError))
        {
            return ErrorResult(deadzoneBottomError!);
        }

        bool debugRects = ResolveBool(
            ResolveBoolString(GetArg(argsMap, "--debug-rects"), environmentReader("CHRONOTAPE_DEBUG_RECTS")),
            config.DebugDrawRects,
            false);

        string outputPath = FirstNonEmpty(
            GetArg(argsMap, "--tape-out"),
            environmentReader("CHRONOTAPE_OUTPUT_PATH"),
            config.OutputPath,
            "./tape.png")!;

        string fontFamily = FirstNonEmpty(
            GetArg(argsMap, "--font-family"),
            environmentReader("CHRONOTAPE_FONT_FAMILY"),
            config.FontFamily,
            "monospace")!;

        string? fontPath = FirstNonEmpty(
            GetArg(argsMap, "--font"),
            environmentReader("CHRONOTAPE_FONT_PATH"),
            config.FontPath);

        if (!string.IsNullOrWhiteSpace(fontPath))
        {
            fontPath = Path.GetFullPath(fontPath);
            if (!File.Exists(fontPath))
            {
                return ErrorResult($"Font file does not exist: {fontPath}");
            }
        }

        var spec = new TapeSpec
        {
            SegmentCharacters = segmentCharacters,
            MainCharacters = mainCharacters,
            Offset = offset,
            SlitCount = slitCount,
            SegmentWidthPx = segmentWidthPx,
            SegmentHeightPx = segmentHeightPx,
            TopMarginPx = topMarginPx,
            DeadzoneRectPx = new SKRectI(deadzoneLeft, deadzoneTop, deadzoneRight, deadzoneBottom),
            FontPath = fontPath,
            FontFamily = fontFamily,
            FontStyle = SKFontStyle.Normal,
            ForegroundColor = SKColors.White,
            BackgroundColor = SKColors.Black,
            MainPaddingPx = mainPaddingPx,
            DeadzonePaddingPx = deadzonePaddingPx,
            OutputPath = outputPath,
            DebugDrawRects = debugRects
        };

        return new TapeGenerationParseResult
        {
            ShouldRun = true,
            Spec = spec
        };
    }

    private static bool TryParseArguments(string[] cliArgs, out Dictionary<string, string> argsMap, out string? error)
    {
        argsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        error = null;

        for (int i = 0; i < cliArgs.Length; i++)
        {
            string arg = cliArgs[i];
            if (arg.Equals(GenerateFlag, StringComparison.OrdinalIgnoreCase)
                || arg.Equals(SampleFlag, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unknown argument: {arg}";
                return false;
            }

            if (!SupportedArguments.Contains(arg))
            {
                error = $"Unknown argument for tape generation: {arg}";
                return false;
            }

            if (arg.Equals("--debug-rects", StringComparison.OrdinalIgnoreCase))
            {
                argsMap[arg] = "true";
                continue;
            }

            if (i + 1 >= cliArgs.Length)
            {
                error = $"Missing value for argument {arg}";
                return false;
            }

            argsMap[arg] = cliArgs[++i];
        }

        return true;
    }

    private static bool TryResolveInt(
        IReadOnlyDictionary<string, string> argsMap,
        Func<string, string?> environmentReader,
        string argumentName,
        string environmentName,
        int? configValue,
        int defaultValue,
        out int value,
        out string? error)
    {
        string? raw = FirstNonEmpty(GetArg(argsMap, argumentName), environmentReader(environmentName));
        if (raw is not null)
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                error = null;
                return true;
            }

            error = $"Invalid integer for {argumentName} / {environmentName}: '{raw}'.";
            return false;
        }

        value = configValue ?? defaultValue;
        error = null;
        return true;
    }

    private static string? GetArg(IReadOnlyDictionary<string, string> argsMap, string key) =>
        argsMap.TryGetValue(key, out string? value) ? value : null;

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool ResolveBool(params bool?[] values)
    {
        foreach (bool? value in values)
        {
            if (value.HasValue)
            {
                return value.Value;
            }
        }

        return false;
    }

    private static bool? ResolveBoolString(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (bool.TryParse(value, out bool parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static TapeGenerationParseResult ErrorResult(string message) =>
        new()
        {
            ShouldRun = true,
            Error = message
        };
}

internal sealed class TapeConfigFile
{
    public string? SegmentCharacters { get; set; }
    public string? MainCharacters { get; set; }
    public int? Offset { get; set; }
    public int? SlitCount { get; set; }
    public int? SegmentWidthPx { get; set; }
    public int? SegmentHeightPx { get; set; }
    public int? TopMarginPx { get; set; }
    public TapeRectConfig? DeadzoneRectPx { get; set; }
    public string? FontPath { get; set; }
    public string? FontFamily { get; set; }
    public int? MainPaddingPx { get; set; }
    public int? DeadzonePaddingPx { get; set; }
    public string? OutputPath { get; set; }
    public bool? DebugDrawRects { get; set; }
}

internal sealed class TapeRectConfig
{
    public int? Left { get; set; }
    public int? Top { get; set; }
    public int? Right { get; set; }
    public int? Bottom { get; set; }
}
