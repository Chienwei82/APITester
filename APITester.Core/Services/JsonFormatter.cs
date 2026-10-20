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

    private static readonly byte[] NewLineBytes = "\n"u8.ToArray();

    /// <summary>
    /// Escribe todos los resultados de un archivo de salida en el formato indicado.
    /// Serializa directo al stream (UTF-8, sin BOM): evita el string intermedio y
    /// deja los archivos listos para cualquier parser JSON/NDJSON.
    /// </summary>
    public static async Task SaveAsync(
        string filePath,
        IReadOnlyList<ApiResponse> results,
        OutputFormat format,
        CancellationToken cancellationToken = default)
    {
        EnsureDirectoryExists(filePath);

        await using var stream = OpenWrite(filePath, FileMode.Create);
        if (format == OutputFormat.Ndjson)
        {
            foreach (var result in results)
            {
                await SerializeLineAsync(stream, result, CompactOptions, cancellationToken).ConfigureAwait(false);
            }
            return;
        }

        if (results.Count == 1)
            await JsonSerializer.SerializeAsync(stream, results[0], IndentedOptions, cancellationToken).ConfigureAwait(false);
        else
            await JsonSerializer.SerializeAsync(stream, results, IndentedOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Agrega una respuesta como linea NDJSON (sin BOM, aunque cree el archivo).</summary>
    public static async Task AppendToFileAsync(
        string filePath,
        ApiResponse result,
        CancellationToken cancellationToken = default)
    {
        EnsureDirectoryExists(filePath);

        await using var stream = OpenWrite(filePath, FileMode.Append);
        await SerializeLineAsync(stream, result, CompactOptions, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SerializeLineAsync(
        Stream stream,
        ApiResponse result,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        await JsonSerializer.SerializeAsync(stream, result, options, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(NewLineBytes, cancellationToken).ConfigureAwait(false);
    }

    private static FileStream OpenWrite(string filePath, FileMode mode) =>
        new(filePath, mode, FileAccess.Write, FileShare.Read, bufferSize: 81920, FileOptions.Asynchronous);

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }
}
