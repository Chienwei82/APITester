using System.Globalization;
using System.Text.Json;

namespace APITester.Core.Services;

/// <summary>
/// Lectura unica y validada de archivos de configuracion: existencia, tamano
/// maximo y contenido no vacio. Compartida por todos los loaders para tener
/// los mismos mensajes de error y no releer el disco dos veces.
/// </summary>
public static class ConfigFileReader
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public static async Task<string> ReadAsync(
        string filePath,
        string singleKeyField,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"No se encuentra '{filePath}'");

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > MaxFileSizeBytes)
            throw new InvalidDataException(
                $"Archivo de configuracion demasiado grande ({Kilobytes(fileInfo.Length)}KB). Limite: {MaxFileSizeBytes / 1024 / 1024}MB");

        var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException(NoRequestsMessage(singleKeyField));

        return json;
    }

    public static string NoRequestsMessage(string singleKeyField) =>
        $"JSON sin requests. Usa '{singleKeyField}' para uno o '[{{ \"{singleKeyField}\": ... }}]' para varios.";

    /// <summary>
    /// Parsea el texto y unifica el mensaje de JSON invalido. El documento
    /// resultante sirve para detectar la forma del archivo y deserializarlo una
    /// sola vez.
    /// </summary>
    public static JsonDocument ParseDocument(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Error deserializando JSON: {ex.Message}", ex);
        }
    }

    private static string Kilobytes(long bytes) =>
        (bytes / 1024.0).ToString("F0", CultureInfo.InvariantCulture);
}
