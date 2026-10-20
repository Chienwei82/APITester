using System.Text;
using System.Text.Json;
using APITester.Core.Models;
using APITester.Core.Services;

namespace APITester.Tests;

public class JsonFormatterTests : IDisposable
{
    private readonly string _tempDir;

    public JsonFormatterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"apitester-json-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
        GC.SuppressFinalize(this);
    }

    private static ApiResponse MakeResponse(string name, int statusCode = 200) => new()
    {
        Request = new RequestInfo { Name = name },
        Response = new ResponseInfo { StatusCode = statusCode, BodyRaw = "{}" }
    };

    private string PathOf(string name) => Path.Combine(_tempDir, name);

    [Fact]
    public async Task SaveAsync_SingleResult_WritesObjectWithoutBom()
    {
        var path = PathOf("single.json");

        await JsonFormatter.SaveAsync(path, [MakeResponse("one")], OutputFormat.Json);

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.False(IsUtf8Bom(bytes));
        using var document = JsonDocument.Parse(bytes);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.Equal("one", document.RootElement.GetProperty("Request").GetProperty("Name").GetString());
    }

    [Fact]
    public async Task SaveAsync_MultipleResults_WritesArrayWithoutBom()
    {
        var path = PathOf("multi.json");

        await JsonFormatter.SaveAsync(path, [MakeResponse("one"), MakeResponse("two")], OutputFormat.Json);

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.False(IsUtf8Bom(bytes));
        using var document = JsonDocument.Parse(bytes);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Equal(2, document.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task SaveAsync_Ndjson_WritesOneJsonDocumentPerLine()
    {
        var path = PathOf("lines.ndjson");

        await JsonFormatter.SaveAsync(path, [MakeResponse("one"), MakeResponse("two")], OutputFormat.Ndjson);

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.False(IsUtf8Bom(bytes));

        var lines = Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        foreach (var line in lines)
        {
            using var document = JsonDocument.Parse(line);
            Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        }
    }

    [Fact]
    public async Task AppendToFileAsync_CreatesFileWithoutBomAndAppendsLines()
    {
        var path = PathOf("append.ndjson");

        await JsonFormatter.AppendToFileAsync(path, MakeResponse("one"));
        await JsonFormatter.AppendToFileAsync(path, MakeResponse("two"));

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.False(IsUtf8Bom(bytes), "El NDJSON no debe empezar con BOM");

        var lines = Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public async Task SaveAsync_CreatesMissingDirectories()
    {
        var path = Path.Combine(_tempDir, "nested", "dir", "out.json");

        await JsonFormatter.SaveAsync(path, [MakeResponse("one")], OutputFormat.Json);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task SaveAsync_OmitsNullProperties()
    {
        var path = PathOf("nulls.json");

        await JsonFormatter.SaveAsync(path, [MakeResponse("one")], OutputFormat.Json);

        var json = await File.ReadAllTextAsync(path);
        Assert.DoesNotContain("\"Error\"", json);
        Assert.DoesNotContain("\"Truncated\"", json);
    }

    private static bool IsUtf8Bom(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
}
