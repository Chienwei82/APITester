using APITester.Core.Services;

namespace APITester.Tests;

public class HeaderRedactorTests
{
    [Fact]
    public void Redact_MasksSensitiveRequestHeaders()
    {
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer secret-token",
            ["Cookie"] = "session=abc123"
        };

        var result = HeaderRedactor.Redact(headers)!;

        Assert.Equal(HeaderRedactor.MaskValue, result["Authorization"]);
        Assert.Equal(HeaderRedactor.MaskValue, result["Cookie"]);
    }

    [Fact]
    public void Redact_MasksSetCookie()
    {
        var headers = new Dictionary<string, string>
        {
            ["Set-Cookie"] = "session=abc123; HttpOnly"
        };

        var result = HeaderRedactor.Redact(headers)!;

        Assert.Equal(HeaderRedactor.MaskValue, result["Set-Cookie"]);
    }

    [Fact]
    public void Redact_KeepsNonSensitiveHeaders()
    {
        var headers = new Dictionary<string, string>
        {
            ["Accept"] = "application/json",
            ["X-Api-Version"] = "2"
        };

        var result = HeaderRedactor.Redact(headers)!;

        Assert.Equal("application/json", result["Accept"]);
        Assert.Equal("2", result["X-Api-Version"]);
    }

    [Fact]
    public void Redact_IsCaseInsensitive()
    {
        var headers = new Dictionary<string, string>
        {
            ["AUTHORIZATION"] = "Bearer secret",
            ["content-type"] = "application/json"
        };

        var result = HeaderRedactor.Redact(headers)!;

        Assert.Equal(HeaderRedactor.MaskValue, result["AUTHORIZATION"]);
        Assert.Equal("application/json", result["content-type"]);
    }

    [Fact]
    public void Redact_Null_ReturnsNull()
    {
        Assert.Null(HeaderRedactor.Redact(null));
    }

    [Fact]
    public void IsSensitive_DetectsKnownHeaders()
    {
        Assert.True(HeaderRedactor.IsSensitive("authorization"));
        Assert.True(HeaderRedactor.IsSensitive("Proxy-Authorization"));
        Assert.False(HeaderRedactor.IsSensitive("X-Custom"));
    }
}
