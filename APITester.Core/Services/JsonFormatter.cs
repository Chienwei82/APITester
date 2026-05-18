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

    public static async Task SaveToFileAsync(string filePath, List<ApiResponse> results)
    {
        object outputData = results.Count == 1 ? results[0] : results;

        var json = JsonSerializer.Serialize(outputData, Options);
        await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
    }
}
