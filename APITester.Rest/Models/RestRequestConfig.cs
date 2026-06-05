using System.Text.Json.Serialization;
using APITester.Core.Models;
using APITester.Core.Services;

namespace APITester.Rest.Models;

public class RestRequestConfig
{
    public string? Name { get; set; }
    public string? Url { get; set; }
    public string Method { get; set; } = "GET";
    public Dictionary<string, string>? Headers { get; set; }
    public Dictionary<string, string>? Query { get; set; }
    public string? Body { get; set; }
    public CertConfig? Cert { get; set; }
    public string? Output { get; set; }

    [JsonPropertyName("timeout")]
    public int TimeoutInSeconds { get; set; } = 30;

    [JsonPropertyName("retries")]
    public int Retries { get; set; } = 0;

    [JsonPropertyName("retryDelayMs")]
    public int RetryDelayMilliseconds { get; set; } = 1000;

    public IEnumerable<string> Validate()
    {
        var urlError = ConfigValidator.ValidateUrl(Url);
        if (urlError is not null) yield return urlError;

        var timeoutError = ConfigValidator.ValidateTimeout(TimeoutInSeconds);
        if (timeoutError is not null) yield return timeoutError;

        var certError = ConfigValidator.ValidateCert(Cert);
        if (certError is not null) yield return certError;

        if (!IsValidMethod(Method))
            yield return $"Metodo HTTP '{Method}' no soportado";

        if (!string.IsNullOrEmpty(Body) && !HasBody(Method))
            yield return $"El metodo '{Method}' no soporta body";
    }

    private static bool HasBody(string method) =>
        method.ToUpperInvariant() is "POST" or "PUT" or "PATCH";

    private static bool IsValidMethod(string method) =>
        method.ToUpperInvariant() is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS";
}
