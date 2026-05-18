using APITester.Core.Models;

namespace APITester.Core.Services;

public static class ConfigValidator
{
    public static string? ValidateTimeout(int timeoutInSeconds)
    {
        if (timeoutInSeconds <= 0)
            return "Timeout debe ser mayor a 0 segundos";
        if (timeoutInSeconds > 300)
            return "Timeout no puede exceder 300 segundos";
        return null;
    }

    public static string? ValidateCert(CertConfig? cert)
    {
        if (cert is null) return null;
        if (string.IsNullOrWhiteSpace(cert.Path))
            return "La ruta del certificado no puede estar vacia";
        if (!File.Exists(cert.Path))
            return $"Certificado no encontrado: {cert.Path}";
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
