using APITester.Rest.Services;

namespace APITester.Tests;

public class RestConfigLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public RestConfigLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"apitester-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Dispose(true);

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task LoadAsync_SingleRequest_LoadsCorrectly()
    {
        var json = """{"name": "Test", "url": "https://api.example.com/items", "method": "GET", "timeout": 60}""";
        var path = WriteJson("single.json", json);

        var result = await RestConfigLoader.LoadAsync(path);

        Assert.Single(result);
        Assert.Equal("Test", result[0].Name);
        Assert.Equal("https://api.example.com/items", result[0].Url);
        Assert.Equal("GET", result[0].Method);
        Assert.Equal(60, result[0].TimeoutInSeconds);
    }

    [Fact]
    public async Task LoadAsync_MultipleRequests_LoadsAll()
    {
        var json = """[{"name": "First", "url": "https://api.example.com/one", "method": "GET"}, {"name": "Second", "url": "https://api.example.com/two", "method": "POST", "body": "{\"key\":\"value\"}"}]""";
        var path = WriteJson("multi.json", json);

        var result = await RestConfigLoader.LoadAsync(path);

        Assert.Equal(2, result.Count);
        Assert.Equal("First", result[0].Name);
        Assert.Equal("Second", result[1].Name);
        Assert.Equal("POST", result[1].Method);
        Assert.Equal("{\"key\":\"value\"}", result[1].Body);
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => RestConfigLoader.LoadAsync("/nonexistent/path.json"));
    }

    [Fact]
    public async Task LoadAsync_EmptyJson_ThrowsInvalidDataException()
    {
        var path = WriteJson("empty.json", "");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => RestConfigLoader.LoadAsync(path));
    }

    [Fact]
    public async Task LoadAsync_WithHeadersAndQuery_ParsesCorrectly()
    {
        var json = """{"name": "Complex", "url": "https://api.example.com/search", "method": "GET", "headers": {"Authorization": "Bearer token123", "X-Custom": "value"}, "query": {"page": "1", "limit": "50"}}""";
        var path = WriteJson("complex.json", json);

        var result = await RestConfigLoader.LoadAsync(path);

        Assert.Single(result);
        Assert.Equal("Bearer token123", result[0].Headers!["Authorization"]);
        Assert.Equal("1", result[0].Query!["page"]);
        Assert.Equal("50", result[0].Query!["limit"]);
    }

    private string WriteJson(string filename, string content)
    {
        var path = Path.Combine(_tempDir, filename);
        File.WriteAllText(path, content);
        return path;
    }
}
