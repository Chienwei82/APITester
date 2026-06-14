using System.Text.Json;
using APITester.Core.Services;
using APITester.Rest.Models;

namespace APITester.Rest.Services;

public static class RestConfigLoader
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<List<RestRequestConfig>> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"No se encuentra '{filePath}'");

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > MaxFileSizeBytes)
            throw new InvalidDataException(
                $"Archivo de configuracion demasiado grande ({fileInfo.Length / 1024.0:F0}KB). Limite: {MaxFileSizeBytes / 1024 / 1024}MB");

        var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("JSON vacio");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Error deserializando JSON: {ex.Message}", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                return await LoadFromArray(json).ConfigureAwait(false);
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                // Try as RestConfigFile (with defaults/requests structure)
                try
                {
                    var configFile = JsonSerializer.Deserialize<RestConfigFile>(json, JsonOptions);
                    if (configFile is not null)
                    {
                        RestRequestConfig.GlobalDefaults = configFile.Defaults;

                        if (configFile.Requests is { Count: > 0 })
                        {
                            foreach (var req in configFile.Requests)
                                req.ApplyDefaults(configFile.Defaults);
                            return configFile.Requests;
                        }
                    }
                }
                catch (JsonException)
                {
                    // Not a RestConfigFile structure, try as single request
                }

                // Try as single request
                try
                {
                    var single = JsonSerializer.Deserialize<RestRequestConfig>(json, JsonOptions);
                    if (single is not null && single.Url is not null)
                        return [single];
                }
                catch (JsonException ex)
                {
                    throw new InvalidDataException($"Error deserializando JSON como objeto: {ex.Message}", ex);
                }
            }
        }

        throw new InvalidDataException(
            "JSON sin requests. Usa 'url' para uno o '[{ \"url\": ... }]' para varios.");
    }

    private static async Task<List<RestRequestConfig>> LoadFromArray(string json)
    {
        try
        {
            var asArray = JsonSerializer.Deserialize<List<RestRequestConfig>>(json, JsonOptions);
            if (asArray is { Count: > 0 })
                return asArray;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Error deserializando JSON como lista: {ex.Message}", ex);
        }

        throw new InvalidDataException("Array JSON vacio o invalido");
    }
}
