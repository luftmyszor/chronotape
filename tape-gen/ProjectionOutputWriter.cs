using System.Globalization;
using System.Text.Json;

internal static class ProjectionOutputWriter
{
    public static void WriteOutputs(string outputPath, List<SlitProjectionResult> results)
    {
        if (results.Count == 0)
        {
            Console.WriteLine("No projected points to write.");
            return;
        }

        string extension = Path.GetExtension(outputPath).ToLowerInvariant();
        bool outputIsSingleFile = extension is ".csv" or ".json";


        Directory.CreateDirectory(outputPath);
        foreach (SlitProjectionResult result in results)
        {
            string csvPath = Path.Combine(outputPath, $"slit-{result.SlitIndex}.csv");
            string jsonPath = Path.Combine(outputPath, $"slit-{result.SlitIndex}.json");
            WriteCsv(csvPath, result);
            WriteJson(jsonPath, result);
            Console.WriteLine($"Wrote {csvPath}");
            Console.WriteLine($"Wrote {jsonPath}");
        }
    }

    private static void WriteCsv(string path, SlitProjectionResult result)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("pixelX,pixelY,displayX,displayY,displayZ,worldX,worldY,worldZ,slitLocalX,slitLocalY");

        foreach (ProjectedPoint point in result.Points)
        {
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{point.PixelX},{point.PixelY},{point.DisplayWorldX},{point.DisplayWorldY},{point.DisplayWorldZ},{point.WorldX},{point.WorldY},{point.WorldZ},{point.SlitLocalX},{point.SlitLocalY}"));
        }
    }

    private static void WriteJson(string path, SlitProjectionResult result)
    {
        string json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(path, json);
    }
}
