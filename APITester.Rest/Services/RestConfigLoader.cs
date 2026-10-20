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

    public static async Task<List<RestRequestConfig>> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var json = await ConfigFileReader.ReadAsync(filePath, singleKeyField: "url", cancellationToken)
            .ConfigureAwait(false);

        // Un solo parseo: el mismo documento sirve para detectar la forma del
        // archivo y para deserializar.
        using var document = ConfigFileReader.ParseDocument(json);

        // Objeto con "defaults"/"requests"/"request" => estructura de archivo REST.
        // Cualquier otro JSON (array o request unico) lo resuelve el loader generico.
        return HasConfigFileStructure(document.RootElement)
            ? LoadFromConfigFile(document.RootElement)
            : GenericLoader.Parse(document);
    }

    private static bool HasConfigFileStructure(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && (root.TryGetProperty("defaults", out _)
            || root.TryGetProperty("requests", out _)
            || root.TryGetProperty("request", out _));

    private static List<RestRequestConfig> LoadFromConfigFile(JsonElement root)
    {
        RestConfigFile? configFile;
        try
        {
            configFile = root.Deserialize<RestConfigFile>(JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Error deserializando JSON: {ex.Message}", ex);
        }

        if (configFile?.Requests is { Count: > 0 } requests)
        {
            // Un array puede traer 'null' entre los elementos: se descartan.
            var usable = new List<RestRequestConfig>(requests.Count);
            foreach (var request in requests)
            {
                if (request is null) continue;
                request.ApplyDefaults(configFile.Defaults);
                usable.Add(request);
            }

            if (usable.Count > 0)
                return usable;
        }

        if (configFile?.Request is not null)
        {
            configFile.Request.ApplyDefaults(configFile.Defaults);
            return [configFile.Request];
        }

        throw new InvalidDataException(ConfigFileReader.NoRequestsMessage("url"));
    }
}
