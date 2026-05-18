using System.Text.Json;

namespace APITester.Core.Services;

public class GenericConfigLoader<T> : IConfigLoader<T> where T : class
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

    public async Task<List<T>> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"No se encuentra '{filePath}'");

        var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException(
                $"JSON sin requests. Usa '{_singleKeyField}' para uno o '[{{ \"{_singleKeyField}\": ... }}]' para varios.");

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
                try
                {
                    var multiple = JsonSerializer.Deserialize<List<T>>(json, JsonOptions);
                    if (multiple is { Count: > 0 })
                        return multiple;
                }
                catch (JsonException ex)
                {
                    throw new InvalidDataException($"Error deserializando JSON como lista: {ex.Message}", ex);
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                try
                {
                    var single = JsonSerializer.Deserialize<T>(json, JsonOptions);
                    if (single is not null && _isSingleValid(single))
                        return [single];
                }
                catch (JsonException ex)
                {
                    throw new InvalidDataException($"Error deserializando JSON como objeto: {ex.Message}", ex);
                }

                // If single doesn't validate, still try as list (for edge cases)
                try
                {
                    var multiple = JsonSerializer.Deserialize<List<T>>(json, JsonOptions);
                    if (multiple is { Count: > 0 })
                        return multiple;
                }
                catch (JsonException ex)
                {
                    throw new InvalidDataException($"Error deserializando JSON como lista: {ex.Message}", ex);
                }
            }
        }

        throw new InvalidDataException(
            $"JSON sin requests. Usa '{_singleKeyField}' para uno o '[{{ \"{_singleKeyField}\": ... }}]' para varios.");
    }
}
