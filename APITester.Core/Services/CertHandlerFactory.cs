using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using APITester.Core.Models;

namespace APITester.Core.Services;

/// <summary>
/// Crea y cachea clientes HTTP con certificado cliente TLS.
/// El HttpClient se cachea por certificado para reutilizar conexiones TLS y
/// evitar filtrar sockets por cada request. La cache es LRU de verdad: cada
/// acceso reciente mueve la clave al final del orden de uso, y cuando se
/// supera el tamaño maximo se desaloja la entrada usada menos recientemente.
/// </summary>
public static class CertHandlerFactory
{
    private const int MaxCacheSize = 10;
    private static readonly object Gate = new();
    private static readonly Dictionary<string, HttpClient> _cache = new();
    private static readonly LinkedList<string> _accessOrder = new();

    public static HttpClient? Create(CertConfig? certConfig)
    {
        if (certConfig?.Path is null) return null;

        var cacheKey = $"{certConfig.Path}|{certConfig.Password ?? ""}";

        lock (Gate)
        {
            if (_cache.TryGetValue(cacheKey, out var existing))
            {
                // Marcar como usada recientemente: mover al final del orden.
                _accessOrder.Remove(cacheKey);
                _accessOrder.AddLast(cacheKey);
                return existing;
            }

            var client = CreateClient(certConfig);
            _cache[cacheKey] = client;
            _accessOrder.AddLast(cacheKey);
            EvictIfNeeded();
            return client;
        }
    }

    private static void EvictIfNeeded()
    {
        while (_cache.Count > MaxCacheSize && _accessOrder.First is not null)
        {
            var oldest = _accessOrder.First.Value;
            _accessOrder.RemoveFirst();
            if (_cache.Remove(oldest, out var removed))
            {
                removed.Dispose();
            }
        }
    }

    private static HttpClient CreateClient(CertConfig certConfig)
    {
        if (string.IsNullOrEmpty(certConfig.Path))
            throw new InvalidOperationException("El certificado no tiene una ruta configurada");

        try
        {
            var cert = string.IsNullOrEmpty(certConfig.Password)
                ? X509CertificateLoader.LoadCertificateFromFile(certConfig.Path)
                : X509CertificateLoader.LoadPkcs12FromFile(certConfig.Path, certConfig.Password);

            var handler = new HttpClientHandler { ClientCertificates = { cert } };
            return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                $"Error cargando certificado '{certConfig.Path}': {ex.Message}", ex);
        }
        catch (FileNotFoundException)
        {
            throw new InvalidOperationException(
                $"Archivo de certificado no encontrado: '{certConfig.Path}'");
        }
    }

    public static void ClearCache()
    {
        lock (Gate)
        {
            foreach (var client in _cache.Values)
            {
                client.Dispose();
            }
            _cache.Clear();
            _accessOrder.Clear();
        }
    }
}