using System.Text.Json;
using APITester.Core.Services;
using APITester.Rest.Models;

namespace APITester.Rest.Services;

public static class RestConfigLoader
{
    /// <summary>
    /// Loader generico reuse que resuelve los casos "array de requests" y
    /// "request unico". RestConfigLoader solo agrega la variante propia REST:
    /// el objeto con "defaults" + "requests".
    /// </summary>
    private static readonly GenericConfigLoader<RestRequestConfig> GenericLoader = new(
        cfg => cfg.Url is not null,
        singleKeyField: "url");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<List<RestRequestConfig>> LoadAsync(string filePath)
    {
        var json = await ConfigFileReader.ReadAsync(filePath, singleKeyField: "url").ConfigureAwait(false);

        // Objeto con "defaults"/"requests"/"request" => estructura de archivo REST.
        // Cualquier otro JSON (array o request unico) lo resuelve el loader generico.
        return HasConfigFileStructure(json) ? LoadFromConfigFile(json) : GenericLoader.Parse(json);
    }

    private static bool HasConfigFileStructure(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return doc.RootElement.TryGetProperty("defaults", out _)
                || doc.RootElement.TryGetProperty("requests", out _)
                || doc.RootElement.TryGetProperty("request", out _);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Error deserializando JSON: {ex.Message}", ex);
        }
    }

    private static List<RestRequestConfig> LoadFromConfigFile(string json)
    {
        RestConfigFile? configFile;
        try
        {
            configFile = JsonSerializer.Deserialize<RestConfigFile>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Error deserializando JSON: {ex.Message}", ex);
        }

        if (configFile?.Requests is { Count: > 0 })
        {
            foreach (var req in configFile.Requests)
            {
                req.ApplyDefaults(configFile.Defaults);
            }
            return configFile.Requests;
        }

        if (configFile?.Request is not null)
        {
            configFile.Request.ApplyDefaults(configFile.Defaults);
            return [configFile.Request];
        }

        throw new InvalidDataException(ConfigFileReader.NoRequestsMessage("url"));
    }
}
