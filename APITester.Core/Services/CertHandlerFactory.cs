using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using APITester.Core.Models;

namespace APITester.Core.Services;

/// <summary>
/// Crea y cachea clientes HTTP con certificado cliente TLS.
/// El HttpClient se cachea por certificado para reutilizar conexiones TLS y
/// evitar filtrar sockets/handlers por cada request.
/// </summary>
public static class CertHandlerFactory
{
    private const int MaxCacheSize = 10;
    private static readonly ConcurrentDictionary<string, HttpClient> _clientCache = new();
    private static readonly ConcurrentQueue<string> _accessOrder = new();

    public static HttpClient? Create(CertConfig? certConfig)
    {
        if (certConfig?.Path is null) return null;

        var cacheKey = $"{certConfig.Path}|{certConfig.Password ?? ""}";

        var client = _clientCache.GetOrAdd(cacheKey, key =>
        {
            EvictIfNeeded();
            _accessOrder.Enqueue(key);
            return CreateClient(certConfig);
        });

        return client;
    }

    private static void EvictIfNeeded()
    {
        while (_clientCache.Count >= MaxCacheSize && _accessOrder.TryDequeue(out var oldest))
        {
            if (_clientCache.TryRemove(oldest, out var removed))
            {
                removed.Dispose();
            }
        }
    }

    private static HttpClient CreateClient(CertConfig certConfig)
    {
        var handler = CreateHandler(certConfig);
        return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    private static HttpClientHandler CreateHandler(CertConfig certConfig)
    {
        if (string.IsNullOrEmpty(certConfig.Path))
            throw new InvalidOperationException("El certificado no tiene una ruta configurada");

        try
        {
            var cert = string.IsNullOrEmpty(certConfig.Password)
                ? X509CertificateLoader.LoadCertificateFromFile(certConfig.Path)
                : X509CertificateLoader.LoadPkcs12FromFile(certConfig.Path, certConfig.Password);

            return new HttpClientHandler { ClientCertificates = { cert } };
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
        foreach (var kv in _clientCache)
        {
            kv.Value.Dispose();
        }
        _clientCache.Clear();
        _accessOrder.Clear();
    }
}