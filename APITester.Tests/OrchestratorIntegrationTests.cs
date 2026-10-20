using System.Text;
using System.Text.Json;
using APITester.Rest;

namespace APITester.Tests;

/// <summary>
/// Tests end-to-end del pipeline (loader -> ejecucion -> guardado) contra un
/// servidor HTTP local: no dependen de Internet ni de APIs publicas.
/// </summary>
public class OrchestratorIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TestHttpServer _server = new();

    public OrchestratorIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"apitester-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        _server.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
        GC.SuppressFinalize(this);
    }

    private string WriteJson(string filename, string content)
    {
        var path = Path.Combine(_tempDir, filename);
        File.WriteAllText(path, content);
        return path;
    }

    private string OutputPath(string filename) => Path.Combine(_tempDir, filename).Replace("\\", "\\\\");

    private string SingleRequestConfig(string output, string method = "GET", string? headers = null)
    {
        var headersJson = headers is null ? string.Empty : $",\n  \"headers\": {headers}";
        return $$"""
            {
              "name": "local",
              "url": "{{_server.Url("ok")}}",
              "method": "{{method}}",
              "output": "{{output}}"{{headersJson}}
            }
            """;
    }

    [Fact]
    public async Task RunAsync_SuccessfulRequest_ReturnsZeroAndWritesOutput()
    {
        var configPath = WriteJson("ok.json", SingleRequestConfig(OutputPath("ok-output.json")));

        var exitCode = await RestOrchestrator.RunAsync(["-c", configPath, "--quiet", "--no-color"]);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(_tempDir, "ok-output.json")));
    }

    [Theory]
    [InlineData(404)]
    [InlineData(500)]
    public async Task RunAsync_ErrorStatus_ReturnsOne(int statusCode)
    {
        _server.StatusCode = statusCode;
        var configPath = WriteJson("error.json", SingleRequestConfig(OutputPath("error-output.json")));

        var exitCode = await RestOrchestrator.RunAsync(["-c", configPath, "--quiet", "--no-color"]);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_RedactsSensitiveHeadersByDefault()
    {
        var configPath = WriteJson("redact.json", SingleRequestConfig(
            OutputPath("redact-output.json"),
            headers: """{ "Authorization": "Bearer super-secret-token" }"""));

        var exitCode = await RestOrchestrator.RunAsync(["-c", configPath, "--quiet", "--no-color"]);

        Assert.Equal(0, exitCode);
        var json = await File.ReadAllTextAsync(Path.Combine(_tempDir, "redact-output.json"));
        Assert.Contains("***", json);
        Assert.DoesNotContain("super-secret-token", json);
        // El request real si viajo con el token.
        Assert.Equal("Bearer super-secret-token", _server.LastAuthorizationHeader);
    }

    [Fact]
    public async Task RunAsync_NoRedactFlag_KeepsSensitiveHeaders()
    {
        var configPath = WriteJson("no-redact.json", SingleRequestConfig(
            OutputPath("no-redact-output.json"),
            headers: """{ "Authorization": "Bearer super-secret-token" }"""));

        var exitCode = await RestOrchestrator.RunAsync(["-c", configPath, "--no-redact", "--quiet", "--no-color"]);

        Assert.Equal(0, exitCode);
        var json = await File.ReadAllTextAsync(Path.Combine(_tempDir, "no-redact-output.json"));
        Assert.Contains("super-secret-token", json);
    }

    [Fact]
    public async Task RunAsync_WritesJsonWithoutBom()
    {
        var configPath = WriteJson("bom.json", SingleRequestConfig(OutputPath("bom-output.json")));

        await RestOrchestrator.RunAsync(["-c", configPath, "--quiet", "--no-color"]);

        var bytes = await File.ReadAllBytesAsync(Path.Combine(_tempDir, "bom-output.json"));
        Assert.False(IsUtf8Bom(bytes), "El JSON de salida no debe llevar BOM");

        using var document = JsonDocument.Parse(bytes);
        Assert.Equal(200, document.RootElement.GetProperty("Response").GetProperty("StatusCode").GetInt32());
    }

    [Fact]
    public async Task RunAsync_Ndjson_WritesOneLinePerResponseWithoutBom()
    {
        var output = OutputPath("lines.ndjson");
        var configPath = WriteJson("lines.json", $$"""
            [
              { "name": "one", "url": "{{_server.Url("a")}}", "output": "{{output}}" },
              { "name": "two", "url": "{{_server.Url("b")}}", "output": "{{output}}" }
            ]
            """);

        var exitCode = await RestOrchestrator.RunAsync(
            ["-c", configPath, "--format", "ndjson", "--quiet", "--no-color"]);

        Assert.Equal(0, exitCode);
        var bytes = await File.ReadAllBytesAsync(Path.Combine(_tempDir, "lines.ndjson"));
        Assert.False(IsUtf8Bom(bytes), "El NDJSON no debe llevar BOM");
        var lines = Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public async Task RunAsync_InvalidConfig_ReturnsError()
    {
        var exitCode = await RestOrchestrator.RunAsync(["-c", "/nonexistent/config.json"]);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_HelpFlag_ReturnsZero()
    {
        var exitCode = await RestOrchestrator.RunAsync(["--help"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_EmptyConfigArray_ReturnsError()
    {
        var configPath = WriteJson("empty-config.json", "[]");

        var exitCode = await RestOrchestrator.RunAsync(["-c", configPath]);

        Assert.Equal(1, exitCode);
    }

    private static bool IsUtf8Bom(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
}
