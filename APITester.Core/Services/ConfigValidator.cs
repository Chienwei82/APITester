using APITester.Core.Models;

namespace APITester.Core.Services;

/// <summary>
/// Limites y validaciones compartidas por el modelo de configuracion y el
/// asistente interactivo: una unica fuente de verdad para los rangos.
/// </summary>
public static class ConfigValidator
{
    public const int MinRetries = 0;
    public const int MaxRetries = 10;
    public const int MinRetryDelayMs = 0;
    public const int MaxRetryDelayMs = 60_000;
    public const int MinTimeoutInSeconds = 1;
    public const int MaxTimeoutInSeconds = 300;
    public const long DefaultMaxBodyBytes = 4 * 1024 * 1024;

    public static string? ValidateTimeout(int timeoutInSeconds)
    {
        if (timeoutInSeconds <= 0)
            return "Timeout debe ser mayor a 0 segundos";
        if (timeoutInSeconds > MaxTimeoutInSeconds)
            return $"Timeout no puede exceder {MaxTimeoutInSeconds} segundos";
        return null;
    }

    public static string? ValidateRetries(int retries) =>
        retries is < MinRetries or > MaxRetries
            ? $"Retries debe estar entre {MinRetries} y {MaxRetries}"
            : null;

    public static string? ValidateRetryDelay(int delayMs) =>
        delayMs is < MinRetryDelayMs or > MaxRetryDelayMs
            ? $"RetryDelayMs debe estar entre {MinRetryDelayMs} y {MaxRetryDelayMs}"
            : null;

    public static string? ValidateMaxBodyBytes(long maxBodyBytes) =>
        maxBodyBytes < 0
            ? "MaxBodyBytes no puede ser negativo"
            : null;

    public static string? ValidateCert(CertConfig? cert)
    {
        if (cert is null) return null;

        // La ruta puede venir de una variable de entorno (${CERT_PATH}).
        var path = EnvVarResolver.Resolve(cert.Path);
        if (string.IsNullOrWhiteSpace(path))
            return "La ruta del certificado no puede estar vacia";
        if (!File.Exists(path))
            return $"Certificado no encontrado: {path}";
        return null;
    }

    public static string? ValidateUrl(string? url, string fieldName = "Url")
    {
        if (string.IsNullOrWhiteSpace(url))
            return $"{fieldName} es requerido";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return $"{fieldName} no es una URL valida";
        if (uri.Scheme is not ("http" or "https"))
            return $"{fieldName} debe usar http o https";
        return null;
    }
}
