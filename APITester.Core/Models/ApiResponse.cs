using System.Text.Json.Nodes;

namespace APITester.Core.Models;

public class ApiResponse
{
    public RequestInfo Request { get; init; } = new();
    public ResponseInfo? Response { get; set; }
    public string? Error { get; set; }
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
    public string? BodyExtracted { get; set; }
    public long TimeMs { get; init; }
    public int SizeBytes { get; init; }
}
