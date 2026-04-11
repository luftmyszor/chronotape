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

        var options = new ProjectionOptions
        {
            FontPath = Path.GetFullPath(fontPath),
            OutPath = Path.GetFullPath(map.TryGetValue("--out", out string? outPath) ? outPath : "./projection-out"),
        };

        return options;
    }

    internal static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project ./tape-gen/tape-gen.csproj -- --generate-tape --segment-characters <chars> --main-characters <chars> [--tape-out ./tape.png]");
        Console.WriteLine();
        Console.WriteLine("Tape generation value sources (precedence):");
        Console.WriteLine("  CLI flags > environment variables > --tape-config JSON > defaults");
        Console.WriteLine("  Required values: SegmentCharacters, MainCharacters");
        Console.WriteLine("  Env vars: CHRONOTAPE_SEGMENT_CHARACTERS, CHRONOTAPE_MAIN_CHARACTERS, CHRONOTAPE_OUTPUT_PATH, ...");
        Console.WriteLine();
        Console.WriteLine("Sample tape (documentation/testing):");
        Console.WriteLine("  dotnet run --project ./tape-gen/tape-gen.csproj -- --sample-tape [--sample-out ./tape.png]");
        Console.WriteLine();
        Console.WriteLine("Projection mode:");
        Console.WriteLine("  dotnet run --project ./tape-gen/tape-gen.csproj -- \\");
        Console.WriteLine("    --font /absolute/path/to/font.ttf --out ./projection-out");
    }

}
