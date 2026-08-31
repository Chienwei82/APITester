using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using APITester.Core.Services;

namespace APITester.Core.Models;

public class ApiResponse : IHasStatusCode
{
    public RequestInfo Request { get; init; } = new();
    public ResponseInfo? Response { get; set; }
    public string? Error { get; set; }
    int IHasStatusCode.StatusCode => Response?.StatusCode ?? 0;

    /// <summary>
    /// Un request es exitoso cuando no hubo error de transporte y el status
    /// es menor a 400: los 4xx/5xx cuentan como fallo (metrics y exit code).
    /// </summary>
    [JsonIgnore]
    public bool IsSuccessful => Error is null && Response is not null && Response.StatusCode < 400;
}

public class RequestInfo
{
    public string? Name { get; init; }
    public string? Url { get; init; }
    public string Method { get; init; } = "GET";
    public Dictionary<string, string>? RequestHeaders { get; set; }
}

public class ResponseInfo
{
    public int StatusCode { get; init; }
    public string? StatusText { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public JsonNode? Body { get; set; }
    public string? BodyRaw { get; init; }
    public long TimeMs { get; init; }
    public long SizeBytes { get; init; }
}
