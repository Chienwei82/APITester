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

        // La ruta y el password pueden venir de variables de entorno.
        var path = EnvVarResolver.Resolve(certConfig.Path)!;
        var password = EnvVarResolver.Resolve(certConfig.Password);
        var cacheKey = $"{path}|{password ?? string.Empty}";

        lock (Gate)
        {
            if (_cache.TryGetValue(cacheKey, out var existing))
            {
                // Marcar como usada recientemente: mover al final del orden.
                _accessOrder.Remove(cacheKey);
                _accessOrder.AddLast(cacheKey);
                return existing;
            }

            var client = CreateClient(path, password);
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

    private static HttpClient CreateClient(string path, string? password)
    {
        if (string.IsNullOrEmpty(path))
            throw new InvalidOperationException("El certificado no tiene una ruta configurada");

        try
        {
            var cert = string.IsNullOrEmpty(password)
                ? X509CertificateLoader.LoadCertificateFromFile(path)
                : X509CertificateLoader.LoadPkcs12FromFile(path, password);

            var handler = new HttpClientHandler { ClientCertificates = { cert } };
            return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                $"Error cargando certificado '{path}': {ex.Message}", ex);
        }
        catch (FileNotFoundException)
        {
            throw new InvalidOperationException(
                $"Archivo de certificado no encontrado: '{path}'");
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