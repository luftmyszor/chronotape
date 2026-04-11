using System.Globalization;

internal static class ProjectionCliParser
{
    public static ProjectionOptions? Parse(string[] cliArgs)
    {
        if (cliArgs.Length == 0)
        {
            return null;
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < cliArgs.Length; i++)
        {
            string arg = cliArgs[i];
            if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase))
            {
                PrintUsage();
                Environment.Exit(0);
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                Console.WriteLine($"Unknown argument: {arg}");
                PrintUsage();
                Environment.Exit(1);
            }

            if (i + 1 >= cliArgs.Length)
            {
                Console.WriteLine($"Missing value for argument {arg}");
                PrintUsage();
                Environment.Exit(1);
            }

            map[arg] = cliArgs[++i];
        }

        if (!map.TryGetValue("--font", out string? fontPath) || string.IsNullOrWhiteSpace(fontPath))
        {
            Console.WriteLine("--font is required when running projection mode.");
            PrintUsage();
            Environment.Exit(1);
        }

        if (!map.TryGetValue("--text", out string? text) || string.IsNullOrWhiteSpace(text))
        {
            Console.WriteLine("--text is required when running projection mode.");
            PrintUsage();
            Environment.Exit(1);
        }

        if (!text.All(char.IsDigit))
        {
            Console.WriteLine("--text must contain only digits 0-9.");
            Environment.Exit(1);
        }

        var options = new ProjectionOptions
        {
            FontPath = Path.GetFullPath(fontPath),
            Text = text,
            FontSize = ParseDouble(map, "--fontSize", 200),
            SampleStep = ParseInt(map, "--sampleStep", 1),
            OutPath = Path.GetFullPath(map.TryGetValue("--out", out string? outPath) ? outPath : "./projection-out"),
            SampleMode = map.TryGetValue("--sampleMode", out string? sampleMode) ? sampleMode : "fill"
        };

        if (options.SampleStep < 1)
        {
            Console.WriteLine("--sampleStep must be >= 1.");
            Environment.Exit(1);
        }

        if (options.FontSize <= 0)
        {
            Console.WriteLine("--fontSize must be > 0.");
            Environment.Exit(1);
        }

        if (map.TryGetValue("--slitIndex", out string? slitIndexRaw))
        {
            if (!int.TryParse(slitIndexRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int slitIndex))
            {
                Console.WriteLine("--slitIndex must be an integer.");
                Environment.Exit(1);
            }

            options.SlitIndex = slitIndex;
        }

        if (!string.Equals(options.SampleMode, "fill", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(options.SampleMode, "edge", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("--sampleMode must be either 'fill' or 'edge'.");
            Environment.Exit(1);
        }

        return options;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project ./tape-gen/tape-gen.csproj -- \\");
        Console.WriteLine("    --font /absolute/path/to/font.ttf --text \"1234\" --fontSize 200 --sampleStep 2 --out ./projection-out [--slitIndex 0] [--sampleMode fill|edge]");
    }

    private static double ParseDouble(Dictionary<string, string> map, string key, double defaultValue)
    {
        if (!map.TryGetValue(key, out string? raw))
        {
            return defaultValue;
        }

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
        {
            Console.WriteLine($"Invalid numeric value for {key}: {raw}");
            Environment.Exit(1);
        }

        return parsed;
    }

    private static int ParseInt(Dictionary<string, string> map, string key, int defaultValue)
    {
        if (!map.TryGetValue(key, out string? raw))
        {
            return defaultValue;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            Console.WriteLine($"Invalid integer value for {key}: {raw}");
            Environment.Exit(1);
        }

        return parsed;
    }
}
