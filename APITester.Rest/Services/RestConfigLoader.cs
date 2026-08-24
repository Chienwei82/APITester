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
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"No se encuentra '{filePath}'");

        var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("JSON vacio");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Objeto con "defaults"/"requests"/"request" => estructura de archivo REST.
        if (root.ValueKind == JsonValueKind.Object)
        {
            var isConfigFile = root.TryGetProperty("defaults", out _)
                            || root.TryGetProperty("requests", out _)
                            || root.TryGetProperty("request", out _);
            if (isConfigFile)
                return LoadFromConfigFile(json);
        }

        // Caso array o request-unico: lo resuelve el loader generico.
        return await GenericLoader.LoadAsync(filePath).ConfigureAwait(false);
    }

    private static List<RestRequestConfig> LoadFromConfigFile(string json)
    {
        var configFile = JsonSerializer.Deserialize<RestConfigFile>(json, JsonOptions)
            ?? throw new InvalidDataException(
                "JSON sin requests. Usa 'url' para uno o '[{ \"url\": ... }]' para varios.");

        if (configFile.Requests is { Count: > 0 })
        {
            foreach (var req in configFile.Requests)
                req.ApplyDefaults(configFile.Defaults);
            return configFile.Requests;
        }

        if (configFile.Request is not null)
        {
            configFile.Request.ApplyDefaults(configFile.Defaults);
            return [configFile.Request];
        }

        throw new InvalidDataException(
            "JSON sin requests. Usa 'url' para uno o '[{ \"url\": ... }]' para varios.");
    }
}