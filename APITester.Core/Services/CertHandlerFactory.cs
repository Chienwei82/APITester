using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using APITester.Core.Models;

namespace APITester.Core.Services;

public static class CertHandlerFactory
{
    private const int MaxCacheSize = 10;
    private static readonly ConcurrentDictionary<string, HttpClientHandler> _handlerCache = new();
    private static readonly ConcurrentQueue<string> _accessOrder = new();

    public static HttpClientHandler? Create(CertConfig? certConfig)
    {
        if (certConfig?.Path is null) return null;

        var cacheKey = $"{certConfig.Path}|{certConfig.Password ?? ""}";

        var handler = _handlerCache.GetOrAdd(cacheKey, key =>
        {
            EvictIfNeeded();
            _accessOrder.Enqueue(key);
            return CreateHandler(certConfig);
        });

        return handler;
    }

    private static void EvictIfNeeded()
    {
        while (_handlerCache.Count >= MaxCacheSize && _accessOrder.TryDequeue(out var oldest))
        {
            if (_handlerCache.TryRemove(oldest, out var removed))
            {
                removed.Dispose();
            }
        }
    }

    private static HttpClientHandler CreateHandler(CertConfig certConfig)
    {
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
        foreach (var kv in _handlerCache)
        {
            kv.Value.Dispose();
        }
        _handlerCache.Clear();
        _accessOrder.Clear();
    }
}
