using System.Text.Json;

namespace APITester.Core.Services;

/// <summary>
/// Interpreta el texto de un archivo de configuracion ya leido: acepta un array
/// de items o un item unico. La lectura del archivo (existencia, tamano, vacio)
/// la hace <see cref="ConfigFileReader"/>.
/// </summary>
public class GenericConfigLoader<T> where T : class
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Func<T, bool> _isSingleValid;
    private readonly string _singleKeyField;

    public GenericConfigLoader(Func<T, bool> isSingleValid, string singleKeyField)
    {
        _isSingleValid = isSingleValid;
        _singleKeyField = singleKeyField;
    }

    public async Task<List<T>> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var json = await ConfigFileReader.ReadAsync(filePath, _singleKeyField, cancellationToken)
            .ConfigureAwait(false);
        return Parse(json);
    }

    /// <summary>Interpreta el texto ya leido: array de items o item unico.</summary>
    public List<T> Parse(string json)
    {
        using var doc = ConfigFileReader.ParseDocument(json);
        return Parse(doc);
    }

    /// <summary>
    /// Interpreta un documento ya parseado: evita volver a leer y parsear el JSON
    /// cuando el llamador ya lo hizo para decidir la forma del archivo.
    /// </summary>
    public List<T> Parse(JsonDocument document)
    {
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            var items = Deserialize<List<T>>(document.RootElement, "lista");
            if (items is { Count: > 0 })
                return items;
        }
        else if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            var single = Deserialize<T>(document.RootElement, "objeto");
            if (single is not null && _isSingleValid(single))
                return [single];
        }

        throw new InvalidDataException(ConfigFileReader.NoRequestsMessage(_singleKeyField));
    }

    private static TValue? Deserialize<TValue>(JsonElement element, string kind) where TValue : class
    {
        try
        {
            return element.Deserialize<TValue>(JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Error deserializando JSON como {kind}: {ex.Message}", ex);
        }
    }
}
