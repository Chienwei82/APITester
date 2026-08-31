using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using APITester.Core.Models;

namespace APITester.Core.Services;

/// <summary>
/// Crea y cachea clientes HTTP con certificado cliente TLS.
/// El HttpClient se cachea por certificado para reutilizar conexiones TLS y
/// evitar filtrar sockets por cada request. La cache es LRU: cada acceso
/// reciente mueve la clave al final del orden de uso, y cuando se supera el
/// tamano maximo se desaloja la entrada usada menos recientemente.
/// </summary>
/// <remarks>
/// Los clientes entregados por <see cref="Create"/> quedan con una referencia
/// activa que el llamador debe liberar con <see cref="Release"/> al terminar.
/// Asi, un cliente desalojado con requests en vuelo no se desecha hasta que
/// su ultima referencia se libera (evitando ObjectDisposedException).
/// </remarks>
public static class CertHandlerFactory
{
    private const int MaxCacheSize = 10;

    private static readonly object Gate = new();
    private static readonly Dictionary<string, CacheEntry> _cache = new();
    private static readonly LinkedList<string> _accessOrder = new();
    private static readonly List<CacheEntry> _evictedInUse = new();

    private sealed class CacheEntry
    {
        public required HttpClient Client { get; init; }
        public int RefCount;
        public bool Evicted;
    }

    /// <summary>
    /// Devuelve un cliente HTTP para el certificado (o null si no hay certificado).
    /// La referencia devuelta debe liberarse con <see cref="Release"/>.
    /// </summary>
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
                existing.RefCount++;
                return existing.Client;
            }

            var entry = new CacheEntry { Client = CreateClient(certConfig), RefCount = 1 };
            _cache[cacheKey] = entry;
            _accessOrder.AddLast(cacheKey);
            EvictIfNeeded();
            return entry.Client;
        }
    }

    /// <summary>
    /// Libera la referencia tomada con <see cref="Create"/>. Si el cliente fue
    /// desalojado de la cache mientras estaba en uso, se desecha aqui.
    /// </summary>
    public static void Release(HttpClient client)
    {
        if (client is null) return;

        lock (Gate)
        {
            CacheEntry? entry = _cache.Values.FirstOrDefault(e => ReferenceEquals(e.Client, client))
                             ?? _evictedInUse.FirstOrDefault(e => ReferenceEquals(e.Client, client));
            if (entry is null) return;

            if (entry.RefCount > 0)
                entry.RefCount--;
            DisposeIfUnreferenced(entry);
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
                if (removed.RefCount > 0)
                {
                    // En uso: diferir el Dispose hasta el ultimo Release.
                    removed.Evicted = true;
                    _evictedInUse.Add(removed);
                }
                else
                {
                    removed.Client.Dispose();
                }
            }
        }
    }

    private static void DisposeIfUnreferenced(CacheEntry entry)
    {
        if (entry.RefCount == 0 && entry.Evicted)
        {
            _evictedInUse.Remove(entry);
            entry.Client.Dispose();
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
            _accessOrder.Clear();

            foreach (var entry in _cache.Values)
            {
                if (entry.RefCount > 0)
                {
                    entry.Evicted = true;
                    _evictedInUse.Add(entry);
                }
                else
                {
                    entry.Client.Dispose();
                }
            }
            _cache.Clear();
        }
    }
}
