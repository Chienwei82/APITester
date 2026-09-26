using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using APITester.Core.Models;

namespace APITester.Core.Services;

public static class JsonFormatter
{
    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Escribe todos los resultados de un archivo de salida en el formato indicado.</summary>
    public static async Task SaveAsync(string filePath, List<ApiResponse> results, OutputFormat format)
    {
        EnsureDirectoryExists(filePath);

        if (format == OutputFormat.Ndjson)
        {
            await using var writer = new StreamWriter(filePath, append: false, Encoding.UTF8);
            foreach (var result in results)
                await writer.WriteLineAsync(JsonSerializer.Serialize(result, CompactOptions)).ConfigureAwait(false);
            return;
        }

        object outputData = results.Count == 1 ? results[0] : results;
        var json = JsonSerializer.Serialize(outputData, IndentedOptions);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8).ConfigureAwait(false);
    }

    public static async Task AppendToFileAsync(string filePath, ApiResponse result)
    {
        var json = JsonSerializer.Serialize(result, CompactOptions);
        EnsureDirectoryExists(filePath);
        await File.AppendAllTextAsync(filePath, json + Environment.NewLine, Encoding.UTF8).ConfigureAwait(false);
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }
}
