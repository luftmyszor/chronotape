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
        "--segment-width-mm",
        "--segment-height",
        "--segment-height-mm",
        "--top-margin",
        "--top-margin-mm",
        "--deadzone-left",
        "--deadzone-left-mm",
        "--deadzone-top",
        "--deadzone-top-mm",
        "--deadzone-right",
        "--deadzone-right-mm",
        "--deadzone-bottom",
        "--deadzone-bottom-mm",
        "--main-padding",
        "--main-padding-mm",
        "--deadzone-padding",
        "--deadzone-padding-mm",
        "--dpi",
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

        if (!TryResolveInt(argsMap, environmentReader, "--slit-count", "CHRONOTAPE_SLIT_COUNT", config.SlitCount, defaultValue: 1, out int slitCount, out string? slitCountError))
        {
            return ErrorResult(slitCountError!);
        }

        if (!TryResolveDpi(argsMap, environmentReader, config.Dpi, out double? dpi, out string? dpiError))
        {
            return ErrorResult(dpiError!);
        }

        if (!TryResolveDimensionPx(
            argsMap,
            environmentReader,
            "--segment-width",
            "CHRONOTAPE_SEGMENT_WIDTH",
            config.SegmentWidthPx,
            "--segment-width-mm",
            "CHRONOTAPE_SEGMENT_WIDTH_MM",
            config.SegmentWidthMm,
            "SegmentWidthMm",
            defaultValue: 140,
            dpi,
            out int segmentWidthPx,
            out string? segmentWidthError))
        {
            return ErrorResult(segmentWidthError!);
        }

        if (!TryResolveDimensionPx(
            argsMap,
            environmentReader,
            "--segment-height",
            "CHRONOTAPE_SEGMENT_HEIGHT",
            config.SegmentHeightPx,
            "--segment-height-mm",
            "CHRONOTAPE_SEGMENT_HEIGHT_MM",
            config.SegmentHeightMm,
            "SegmentHeightMm",
            defaultValue: 210,
            dpi,
            out int segmentHeightPx,
            out string? segmentHeightError))
        {
            return ErrorResult(segmentHeightError!);
        }

        if (!TryResolveDimensionPx(
            argsMap,
            environmentReader,
            "--top-margin",
            "CHRONOTAPE_TOP_MARGIN",
            config.TopMarginPx,
            "--top-margin-mm",
            "CHRONOTAPE_TOP_MARGIN_MM",
            config.TopMarginMm,
            "TopMarginMm",
            defaultValue: 30,
            dpi,
            out int topMarginPx,
            out string? topMarginError))
        {
            return ErrorResult(topMarginError!);
        }

        if (!TryResolveDimensionPx(
            argsMap,
            environmentReader,
            "--main-padding",
            "CHRONOTAPE_MAIN_PADDING",
            config.MainPaddingPx,
            "--main-padding-mm",
            "CHRONOTAPE_MAIN_PADDING_MM",
            config.MainPaddingMm,
            "MainPaddingMm",
            defaultValue: 8,
            dpi,
            out int mainPaddingPx,
            out string? mainPaddingError))
        {
            return ErrorResult(mainPaddingError!);
        }

        if (!TryResolveDimensionPx(
            argsMap,
            environmentReader,
            "--deadzone-padding",
            "CHRONOTAPE_DEADZONE_PADDING",
            config.DeadzonePaddingPx,
            "--deadzone-padding-mm",
            "CHRONOTAPE_DEADZONE_PADDING_MM",
            config.DeadzonePaddingMm,
            "DeadzonePaddingMm",
            defaultValue: 2,
            dpi,
            out int deadzonePaddingPx,
            out string? deadzonePaddingError))
        {
            return ErrorResult(deadzonePaddingError!);
        }

        if (!TryResolveDimensionPx(
            argsMap,
            environmentReader,
            "--deadzone-left",
            "CHRONOTAPE_DEADZONE_LEFT",
            config.DeadzoneRectPx?.Left,
            "--deadzone-left-mm",
            "CHRONOTAPE_DEADZONE_LEFT_MM",
            config.DeadzoneRectMm?.Left,
            "DeadzoneRectMm.Left",
            defaultValue: 52,
            dpi,
            out int deadzoneLeft,
            out string? deadzoneLeftError))
        {
            return ErrorResult(deadzoneLeftError!);
        }

        if (!TryResolveDimensionPx(
            argsMap,
            environmentReader,
            "--deadzone-top",
            "CHRONOTAPE_DEADZONE_TOP",
            config.DeadzoneRectPx?.Top,
            "--deadzone-top-mm",
            "CHRONOTAPE_DEADZONE_TOP_MM",
            config.DeadzoneRectMm?.Top,
            "DeadzoneRectMm.Top",
            defaultValue: 148,
            dpi,
            out int deadzoneTop,
            out string? deadzoneTopError))
        {
            return ErrorResult(deadzoneTopError!);
        }

        if (!TryResolveDimensionPx(
            argsMap,
            environmentReader,
            "--deadzone-right",
            "CHRONOTAPE_DEADZONE_RIGHT",
            config.DeadzoneRectPx?.Right,
            "--deadzone-right-mm",
            "CHRONOTAPE_DEADZONE_RIGHT_MM",
            config.DeadzoneRectMm?.Right,
            "DeadzoneRectMm.Right",
            defaultValue: 88,
            dpi,
            out int deadzoneRight,
            out string? deadzoneRightError))
        {
            return ErrorResult(deadzoneRightError!);
        }

        if (!TryResolveDimensionPx(
            argsMap,
            environmentReader,
            "--deadzone-bottom",
            "CHRONOTAPE_DEADZONE_BOTTOM",
            config.DeadzoneRectPx?.Bottom,
            "--deadzone-bottom-mm",
            "CHRONOTAPE_DEADZONE_BOTTOM_MM",
            config.DeadzoneRectMm?.Bottom,
            "DeadzoneRectMm.Bottom",
            defaultValue: 184,
            dpi,
            out int deadzoneBottom,
            out string? deadzoneBottomError))
        {
            return ErrorResult(deadzoneBottomError!);
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

    private static bool TryResolveDpi(
        IReadOnlyDictionary<string, string> argsMap,
        Func<string, string?> environmentReader,
        double? configDpi,
        out double? value,
        out string? error)
    {
        string? raw = FirstNonEmpty(GetArg(argsMap, "--dpi"), environmentReader("CHRONOTAPE_DPI"));
        if (raw is not null)
        {
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            {
                value = null;
                error = $"Invalid number for --dpi / CHRONOTAPE_DPI: '{raw}'.";
                return false;
            }

            if (!IsFiniteAndPositive(parsed))
            {
                value = null;
                error = "--dpi / CHRONOTAPE_DPI must be > 0.";
                return false;
            }

            value = parsed;
            error = null;
            return true;
        }

        if (configDpi.HasValue)
        {
            if (!IsFiniteAndPositive(configDpi.Value))
            {
                value = null;
                error = "Dpi in --tape-config must be > 0.";
                return false;
            }

            value = configDpi.Value;
            error = null;
            return true;
        }

        value = null;
        error = null;
        return true;
    }

    private static bool TryResolveDimensionPx(
        IReadOnlyDictionary<string, string> argsMap,
        Func<string, string?> environmentReader,
        string pxArgumentName,
        string pxEnvironmentName,
        int? configPxValue,
        string mmArgumentName,
        string mmEnvironmentName,
        double? configMmValue,
        string configMmLabel,
        int defaultValue,
        double? dpi,
        out int value,
        out string? error)
    {
        string? cliMm = GetArg(argsMap, mmArgumentName);
        if (!string.IsNullOrWhiteSpace(cliMm))
        {
            return TryConvertMillimetersToPixels(cliMm, mmArgumentName, dpi, out value, out error);
        }

        string? cliPx = GetArg(argsMap, pxArgumentName);
        if (!string.IsNullOrWhiteSpace(cliPx))
        {
            return TryParseInteger(cliPx, $"{pxArgumentName} / {pxEnvironmentName}", out value, out error);
        }

        string? envMm = environmentReader(mmEnvironmentName);
        if (!string.IsNullOrWhiteSpace(envMm))
        {
            return TryConvertMillimetersToPixels(envMm, mmEnvironmentName, dpi, out value, out error);
        }

        string? envPx = environmentReader(pxEnvironmentName);
        if (!string.IsNullOrWhiteSpace(envPx))
        {
            return TryParseInteger(envPx, $"{pxArgumentName} / {pxEnvironmentName}", out value, out error);
        }

        if (configMmValue.HasValue)
        {
            return TryConvertMillimetersToPixels(configMmValue.Value, configMmLabel, dpi, out value, out error);
        }

        if (configPxValue.HasValue)
        {
            value = configPxValue.Value;
            error = null;
            return true;
        }

        value = defaultValue;
        error = null;
        return true;
    }

    private static bool TryParseInteger(string raw, string name, out int value, out string? error)
    {
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            error = null;
            return true;
        }

        error = $"Invalid integer for {name}: '{raw}'.";
        return false;
    }

    private static bool TryConvertMillimetersToPixels(string rawMm, string mmName, double? dpi, out int value, out string? error)
    {
        if (!double.TryParse(rawMm, NumberStyles.Float, CultureInfo.InvariantCulture, out double mm))
        {
            value = 0;
            error = $"Invalid millimeter value for {mmName}: '{rawMm}'.";
            return false;
        }

        return TryConvertMillimetersToPixels(mm, mmName, dpi, out value, out error);
    }

    private static bool TryConvertMillimetersToPixels(double millimeters, string mmName, double? dpi, out int value, out string? error)
    {
        if (!double.IsFinite(millimeters))
        {
            value = 0;
            error = $"Invalid millimeter value for {mmName}.";
            return false;
        }

        if (!dpi.HasValue)
        {
            value = 0;
            error = $"{mmName} requires DPI. Provide --dpi, CHRONOTAPE_DPI, or Dpi in --tape-config.";
            return false;
        }

        try
        {
            value = checked((int)Math.Round((millimeters * dpi.Value) / 25.4d, MidpointRounding.AwayFromZero));
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
    public int? SlitCount { get; set; }
    public int? SegmentWidthPx { get; set; }
    public double? SegmentWidthMm { get; set; }
    public int? SegmentHeightPx { get; set; }
    public double? SegmentHeightMm { get; set; }
    public int? TopMarginPx { get; set; }
    public double? TopMarginMm { get; set; }
    public TapeRectConfig? DeadzoneRectPx { get; set; }
    public TapeRectMmConfig? DeadzoneRectMm { get; set; }
    public string? FontPath { get; set; }
    public string? FontFamily { get; set; }
    public int? MainPaddingPx { get; set; }
    public double? MainPaddingMm { get; set; }
    public int? DeadzonePaddingPx { get; set; }
    public double? DeadzonePaddingMm { get; set; }
    public double? Dpi { get; set; }
    public string? OutputPath { get; set; }
    public bool? DebugDrawRects { get; set; }
    public bool? DebugHighlightRects { get; set; }
}

internal sealed class TapeRectConfig
{
    public int? Left { get; set; }
    public int? Top { get; set; }
    public int? Right { get; set; }
    public int? Bottom { get; set; }
}

internal sealed class TapeRectMmConfig
{
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double? Right { get; set; }
    public double? Bottom { get; set; }
}
