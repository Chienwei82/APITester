using APITester.Rest;

namespace APITester.Tests;

public class OrchestratorIntegrationTests : IDisposable
{
    private readonly string _tempDir;

    public OrchestratorIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"apitester-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string WriteJson(string filename, string content)
    {
        var path = Path.Combine(_tempDir, filename);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task RunAsync_SingleRequest_ReturnsSuccess()
    {
        var configPath = WriteJson("test-config.json",
            "{\"url\": \"https://jsonplaceholder.typicode.com/posts/1\", \"method\": \"GET\"}");

        var orchestrator = new RestOrchestrator();
        var exitCode = await orchestrator.RunAsync(["-c", configPath]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_InvalidConfig_ReturnsError()
    {
        var orchestrator = new RestOrchestrator();
        var exitCode = await orchestrator.RunAsync(["-c", "/nonexistent/config.json"]);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_HelpFlag_ReturnsZero()
    {
        var orchestrator = new RestOrchestrator();
        var exitCode = await orchestrator.RunAsync(["--help"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_EmptyConfigArray_ReturnsError()
    {
        var configPath = WriteJson("empty-config.json", "[]");

        var orchestrator = new RestOrchestrator();
        var exitCode = await orchestrator.RunAsync(["-c", configPath]);

        Assert.Equal(1, exitCode);
    }
}
