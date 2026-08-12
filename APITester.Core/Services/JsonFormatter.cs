using System.Text;
using System.Text.Json;
using APITester.Core.Models;

namespace APITester.Core.Services;

public static class JsonFormatter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions NdjsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task SaveToFileAsync(string filePath, List<ApiResponse> results)
    {
        object outputData = results.Count == 1 ? results[0] : results;

        var json = JsonSerializer.Serialize(outputData, Options);
        EnsureDirectoryExists(filePath);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8).ConfigureAwait(false);
    }

    public static async Task SaveToFileNdjsonAsync(string filePath, List<ApiResponse> results)
    {
        EnsureDirectoryExists(filePath);
        using var writer = new StreamWriter(filePath, append: false, Encoding.UTF8);

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];

            var json = JsonSerializer.Serialize(result, NdjsonOptions);
            await writer.WriteLineAsync(json).ConfigureAwait(false);
        }
    }

    public static async Task AppendToFileAsync(string filePath, ApiResponse result)
    {
        var json = JsonSerializer.Serialize(result, NdjsonOptions);
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
