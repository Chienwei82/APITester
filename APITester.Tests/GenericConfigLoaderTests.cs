using APITester.Core.Services;
using APITester.Rest.Models;

namespace APITester.Tests;

public class GenericConfigLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public GenericConfigLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"apitester-generic-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private readonly GenericConfigLoader<RestRequestConfig> _loader = new(
        cfg => cfg.Url is not null,
        "url");

    private string WriteJson(string filename, string content)
    {
        var path = Path.Combine(_tempDir, filename);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task SingleValidObject_ReturnsListWithOneItem()
    {
        var path = WriteJson("valid.json",
            """{"url": "https://api.example.com", "method": "GET"}""");

        var result = await _loader.LoadAsync(path);

        Assert.Single(result);
        Assert.Equal("https://api.example.com", result[0].Url);
        Assert.Equal("GET", result[0].Method);
    }

    [Fact]
    public async Task JsonArray_ReturnsAllItems()
    {
        var path = WriteJson("array.json",
            """[{"url": "https://api.example.com/1"}, {"url": "https://api.example.com/2"}]""");

        var result = await _loader.LoadAsync(path);

        Assert.Equal(2, result.Count);
        Assert.Equal("https://api.example.com/1", result[0].Url);
        Assert.Equal("https://api.example.com/2", result[1].Url);
    }

    [Fact]
    public async Task ArrayWithTimeoutAlias_ParsesCorrectly()
    {
        var path = WriteJson("alias.json",
            """[{"url": "https://api.example.com", "timeout": 45}]""");

        var result = await _loader.LoadAsync(path);

        Assert.Single(result);
        Assert.Equal(45, result[0].TimeoutInSeconds);
    }

    [Fact]
    public async Task EmptyJsonArray_ThrowsInvalidDataException()
    {
        var path = WriteJson("empty.json", "[]");

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => _loader.LoadAsync(path));
        Assert.Contains("JSON sin requests", ex.Message);
    }

    [Fact]
    public async Task MalformedJson_ThrowsInvalidDataException()
    {
        var path = WriteJson("bad.json", "{invalid json here}");

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => _loader.LoadAsync(path));
        Assert.Contains("Error deserializando JSON", ex.Message);
    }

    [Fact]
    public async Task WhitespaceOnly_ThrowsInvalidDataException()
    {
        var path = WriteJson("whitespace.json", "   ");

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => _loader.LoadAsync(path));
        Assert.Contains("JSON sin requests", ex.Message);
    }

    [Fact]
    public async Task JsonStringRoot_ThrowsInvalidDataException()
    {
        var path = WriteJson("string.json", "\"just a string\"");

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => _loader.LoadAsync(path));
        Assert.Contains("JSON sin requests", ex.Message);
    }

    [Fact]
    public async Task JsonNumberRoot_ThrowsInvalidDataException()
    {
        var path = WriteJson("number.json", "42");

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => _loader.LoadAsync(path));
        Assert.Contains("JSON sin requests", ex.Message);
    }

    [Fact]
    public async Task ObjectWithoutRequiredUrl_ThrowsInvalidDataException()
    {
        var path = WriteJson("nourl.json",
            """{"name": "No URL", "method": "GET"}""");

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => _loader.LoadAsync(path));
        // Falls through single-object check (valid JSON but no url),
        // then tries as list, which fails because root is object not array
        Assert.Contains("Error deserializando JSON como lista", ex.Message);
    }

    [Fact]
    public async Task JsonWithExtraWhitespace_ParsesCorrectly()
    {
        var path = WriteJson("pretty.json",
            """
            {
              "url": "https://api.example.com",
              "method": "POST",
              "timeout": 10
            }
            """);

        var result = await _loader.LoadAsync(path);

        Assert.Single(result);
        Assert.Equal("https://api.example.com", result[0].Url);
        Assert.Equal("POST", result[0].Method);
        Assert.Equal(10, result[0].TimeoutInSeconds);
    }

    [Fact]
    public async Task ArrayWithOneInvalidItem_StillReturnsValidItems()
    {
        // Array items are NOT individually validated by GenericConfigLoader
        // (validation happens separately via Validate()). So even items
        // without a URL should be deserialized.
        var path = WriteJson("mixed.json",
            """[{"url": "https://api.example.com/1"}, {"name": "missing-url"}]""");

        var result = await _loader.LoadAsync(path);

        Assert.Equal(2, result.Count);
        Assert.Equal("https://api.example.com/1", result[0].Url);
        Assert.Null(result[1].Url);
    }
}
