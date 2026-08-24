using System.Text.Json.Serialization;
using APITester.Core.Models;
using APITester.Core.Services;

namespace APITester.Rest.Models;

public class RestRequestConfig
{
    public static RestConfigDefaults? GlobalDefaults { get; set; }

    public string? Name { get; set; }
    public string? Url { get; set; }
    public string Method { get; set; } = "GET";
    public Dictionary<string, string>? Headers { get; set; }
    public Dictionary<string, string>? Query { get; set; }
    public string? Body { get; set; }
    public CertConfig? Cert { get; set; }
    public string? Output { get; set; }
    public bool AppendOutput { get; set; }

    [JsonPropertyName("timeout")]
    public int? TimeoutInSeconds { get; set; }

    [JsonPropertyName("retries")]
    public int? Retries { get; set; }

    [JsonPropertyName("retryDelayMs")]
    public int? RetryDelayMilliseconds { get; set; }

    [JsonPropertyName("retryExponentialBackoff")]
    public bool UseExponentialBackoff { get; set; } = false;

    [JsonPropertyName("retryOnStatusCodes")]
    public List<int>? RetryOnStatusCodes { get; set; }

    public void ApplyDefaults(RestConfigDefaults? defaults)
    {
        if (defaults is null) return;

        if (Headers is null && defaults.Headers is not null)
            Headers = defaults.Headers;
        if (Query is null && defaults.Query is not null)
            Query = defaults.Query;
        if (TimeoutInSeconds is null && defaults.TimeoutInSeconds.HasValue)
            TimeoutInSeconds = defaults.TimeoutInSeconds.Value;
        if (Retries is null && defaults.Retries.HasValue)
            Retries = defaults.Retries.Value;
        if (RetryDelayMilliseconds is null && defaults.RetryDelayMilliseconds.HasValue)
            RetryDelayMilliseconds = defaults.RetryDelayMilliseconds.Value;
        if (!string.IsNullOrEmpty(defaults.BaseUrl) && Url is not null && !Url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            Url = defaults.BaseUrl.TrimEnd('/') + "/" + Url.TrimStart('/');
    }

    public IEnumerable<string> Validate()
    {
        var urlError = ConfigValidator.ValidateUrl(Url);
        if (urlError is not null) yield return urlError;

        var timeoutError = ConfigValidator.ValidateTimeout(EffectiveTimeoutInSeconds);
        if (timeoutError is not null) yield return timeoutError;

        var certError = ConfigValidator.ValidateCert(Cert);
        if (certError is not null) yield return certError;

        if (!IsValidMethod(Method))
            yield return $"Metodo HTTP '{Method}' no soportado";

        if (!string.IsNullOrEmpty(Body) && !HasBody(Method))
            yield return $"El metodo '{Method}' no soporta body";
    }

    /// <summary>Efectivo timeout, aplicando el default cuando no se especifico.</summary>
    public int EffectiveTimeoutInSeconds => TimeoutInSeconds ?? 30;

    /// <summary>Efectivos reintentos, aplicando el default cuando no se especifico.</summary>
    public int EffectiveRetries => Retries ?? 0;

    /// <summary>Efectivo delay entre reintentos, aplicando el default cuando no se especifico.</summary>
    public int EffectiveRetryDelayMilliseconds => RetryDelayMilliseconds ?? 1000;

    private static bool HasBody(string method) =>
        method.ToUpperInvariant() is "POST" or "PUT" or "PATCH";

    private static bool IsValidMethod(string method) =>
        method.ToUpperInvariant() is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS";
}

public class RestConfigDefaults
{
    public string? BaseUrl { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public Dictionary<string, string>? Query { get; set; }

    [JsonPropertyName("timeout")]
    public int? TimeoutInSeconds { get; set; }

    [JsonPropertyName("retries")]
    public int? Retries { get; set; }

    [JsonPropertyName("retryDelayMs")]
    public int? RetryDelayMilliseconds { get; set; }
}

public class RestConfigFile
{
    public RestConfigDefaults? Defaults { get; set; }
    public List<RestRequestConfig>? Requests { get; set; }
    public RestRequestConfig? Request { get; set; }
}
