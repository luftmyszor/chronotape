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
    private const double DefaultDpi = 100d;
    private const double DefaultSegmentWidthMm = 35.56d;
    private const double DefaultSegmentHeightMm = 53.34d;
    private const double DefaultTopMarginMm = 7.62d;
    private const double DefaultMainHorizontalPaddingMm = 2.032d;
    private const double DefaultMainVerticalPaddingMm = 2.032d;
    private const double DefaultDeadzoneHorizontalPaddingMm = 0.508d;
    private const double DefaultDeadzoneVerticalPaddingMm = 0.508d;
    private const double DefaultSlitCenterYOffsetMm = 21.844d;
    private static readonly HashSet<string> SupportedArguments =
    [
        GenerateFlag,
        "--tape-config",
        "--world-geometry",
        "--tape-out",
        "--segment-characters",
        "--main-characters",
        "--offset",
        "--slit-count",
        "--font",
        "--font-family",
        "--debug-rects",
        "--highlight-rects"
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

        string worldGeometryPath = FirstNonEmpty(
            GetArg(argsMap, "--world-geometry"),
            environmentReader("CHRONOTAPE_WORLD_GEOMETRY"),
            WorldGeometryConfigLoader.ResolveDefaultPath())!;
        if (!WorldGeometryConfigLoader.TryLoad(worldGeometryPath, out WorldGeometryConfig? worldGeometry, out string? worldGeometryError))
        {
            return ErrorResult(worldGeometryError!);
        }

        if (!TryResolveInt(argsMap, environmentReader, "--slit-count", "CHRONOTAPE_SLIT_COUNT", null, defaultValue: worldGeometry!.SlitCount, out int slitCount, out string? slitCountError))
        {
            return ErrorResult(slitCountError!);
        }

        if (slitCount != worldGeometry!.SlitCount)
        {
            return ErrorResult($"Slit count must match world geometry ({worldGeometry.SlitCount}). Update --world-geometry instead of using --slit-count.");
        }

        double dpi = config.Dpi ?? DefaultDpi;
        if (!IsFiniteAndPositive(dpi))
        {
            return ErrorResult("Dpi in --tape-config must be > 0.");
        }

        if (!TryResolveDimensionPxFromConfig(
            config.SegmentWidthMm,
            "SegmentWidthMm",
            defaultValueMm: DefaultSegmentWidthMm,
            dpi,
            out int segmentWidthPx,
            out string? segmentWidthError))
        {
            return ErrorResult(segmentWidthError!);
        }

        if (!TryResolveDimensionPxFromConfig(
            config.SegmentHeightMm,
            "SegmentHeightMm",
            defaultValueMm: DefaultSegmentHeightMm,
            dpi,
            out int segmentHeightPx,
            out string? segmentHeightError))
        {
            return ErrorResult(segmentHeightError!);
        }

        if (!TryResolveDimensionPxFromConfig(
            config.TopMarginMm,
            "TopMarginMm",
            defaultValueMm: DefaultTopMarginMm,
            dpi,
            out int topMarginPx,
            out string? topMarginError))
        {
            return ErrorResult(topMarginError!);
        }

        double? mainHorizontalPaddingMm = config.MainHorizontalPaddingMm ?? config.MainPaddingMm;
        double? mainVerticalPaddingMm = config.MainVerticalPaddingMm ?? config.MainPaddingMm;
        double? deadzoneHorizontalPaddingMm = config.DeadzoneHorizontalPaddingMm ?? config.DeadzonePaddingMm;
        double? deadzoneVerticalPaddingMm = config.DeadzoneVerticalPaddingMm ?? config.DeadzonePaddingMm;

        if (!TryResolveDimensionPxFromConfig(
            mainHorizontalPaddingMm,
            "MainHorizontalPaddingMm",
            defaultValueMm: DefaultMainHorizontalPaddingMm,
            dpi,
            out int mainPaddingXPx,
            out string? mainPaddingXError))
        {
            return ErrorResult(mainPaddingXError!);
        }

        if (!TryResolveDimensionPxFromConfig(
            mainVerticalPaddingMm,
            "MainVerticalPaddingMm",
            defaultValueMm: DefaultMainVerticalPaddingMm,
            dpi,
            out int mainPaddingYPx,
            out string? mainPaddingYError))
        {
            return ErrorResult(mainPaddingYError!);
        }

        if (!TryResolveDimensionPxFromConfig(
            deadzoneHorizontalPaddingMm,
            "DeadzoneHorizontalPaddingMm",
            defaultValueMm: DefaultDeadzoneHorizontalPaddingMm,
            dpi,
            out int deadzonePaddingXPx,
            out string? deadzonePaddingXError))
        {
            return ErrorResult(deadzonePaddingXError!);
        }

        if (!TryResolveDimensionPxFromConfig(
            deadzoneVerticalPaddingMm,
            "DeadzoneVerticalPaddingMm",
            defaultValueMm: DefaultDeadzoneVerticalPaddingMm,
            dpi,
            out int deadzonePaddingYPx,
            out string? deadzonePaddingYError))
        {
            return ErrorResult(deadzonePaddingYError!);
        }

        if (!TryResolveDimensionPxFromConfig(
            worldGeometry.SlitWidthMm,
            "SlitWidthMm",
            defaultValueMm: worldGeometry.SlitWidthMm,
            dpi,
            out int slitWidthPx,
            out string? slitWidthError))
        {
            return ErrorResult(slitWidthError!);
        }

        if (!TryResolveDimensionPxFromConfig(
            worldGeometry.SlitHeightMm,
            "SlitHeightMm",
            defaultValueMm: worldGeometry.SlitHeightMm,
            dpi,
            out int slitHeightPx,
            out string? slitHeightError))
        {
            return ErrorResult(slitHeightError!);
        }

        if (!TryResolveDimensionPxFromConfig(
            config.SlitCenterYOffsetMm,
            "SlitCenterYOffsetMm",
            defaultValueMm: DefaultSlitCenterYOffsetMm,
            dpi,
            out int slitCenterYOffsetPx,
            out string? slitCenterYOffsetError))
        {
            return ErrorResult(slitCenterYOffsetError!);
        }

        bool debugRects = ResolveBool(
            ResolveBoolString(GetArg(argsMap, "--debug-rects"), environmentReader("CHRONOTAPE_DEBUG_RECTS")),
            config.DebugDrawRects,
            false);

        bool highlightRects = ResolveBool(
            ResolveBoolString(GetArg(argsMap, "--highlight-rects"), environmentReader("CHRONOTAPE_HIGHLIGHT_RECTS")),
            config.DebugHighlightRects,
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
            SlitCount = worldGeometry.SlitCount,
            SegmentWidthPx = segmentWidthPx,
            SegmentHeightPx = segmentHeightPx,
            TopMarginPx = topMarginPx,
            SlitWidthPx = slitWidthPx,
            SlitHeightPx = slitHeightPx,
            SlitCenterYOffsetPx = slitCenterYOffsetPx,
            FontPath = fontPath,
            FontFamily = fontFamily,
            FontStyle = SKFontStyle.Normal,
            ForegroundColor = SKColors.White,
            BackgroundColor = SKColors.Black,
            MainPaddingXPx = mainPaddingXPx,
            MainPaddingYPx = mainPaddingYPx,
            DeadzonePaddingXPx = deadzonePaddingXPx,
            DeadzonePaddingYPx = deadzonePaddingYPx,
            OutputPath = outputPath,
            DebugDrawRects = debugRects,
            DebugHighlightRects = highlightRects
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

            if (arg.Equals("--debug-rects", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--highlight-rects", StringComparison.OrdinalIgnoreCase))
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

    private static bool TryResolveDimensionPxFromConfig(
        double? configuredMillimeters,
        string mmName,
        double defaultValueMm,
        double dpi,
        out int value,
        out string? error)
    {
        double millimeters = configuredMillimeters ?? defaultValueMm;
        return TryConvertMillimetersToPixels(millimeters, mmName, dpi, out value, out error);
    }

    private static bool TryConvertMillimetersToPixels(double millimeters, string mmName, double dpi, out int value, out string? error)
    {
        if (!double.IsFinite(millimeters))
        {
            value = 0;
            error = $"Invalid millimeter value for {mmName}.";
            return false;
        }

        if (!IsFiniteAndPositive(dpi))
        {
            value = 0;
            error = "Dpi in --tape-config must be > 0.";
            return false;
        }

        try
        {
            value = checked((int)Math.Round((millimeters * dpi) / 25.4d, MidpointRounding.AwayFromZero));
            error = null;
            return true;
        }
        catch (OverflowException)
        {
            value = 0;
            error = $"Converted pixel value is out of range for {mmName}.";
            return false;
        }
    }

    private static bool IsFiniteAndPositive(double value) =>
        double.IsFinite(value) && value > 0d;

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
    public double? SegmentWidthMm { get; set; }
    public double? SegmentHeightMm { get; set; }
    public double? TopMarginMm { get; set; }
    public string? FontPath { get; set; }
    public string? FontFamily { get; set; }
    public double? MainHorizontalPaddingMm { get; set; }
    public double? MainVerticalPaddingMm { get; set; }
    public double? DeadzoneHorizontalPaddingMm { get; set; }
    public double? DeadzoneVerticalPaddingMm { get; set; }
    public double? MainPaddingMm { get; set; }
    public double? DeadzonePaddingMm { get; set; }
    public double? SlitCenterYOffsetMm { get; set; }
    public double? Dpi { get; set; }
    public string? OutputPath { get; set; }
    public bool? DebugDrawRects { get; set; }
    public bool? DebugHighlightRects { get; set; }
}
