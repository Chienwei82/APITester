using APITester.Rest.Models;

namespace APITester.Tests;

public class ModelValidationTests
{
    [Fact]
    public void RestRequestConfig_ValidConfig_NoWarnings()
    {
        var config = new RestRequestConfig
        {
            Url = "https://api.example.com",
            Method = "GET",
            TimeoutInSeconds = 30
        };

        var warnings = config.Validate().ToList();

        Assert.Empty(warnings);
    }

    [Fact]
    public void RestRequestConfig_MissingUrl_HasWarning()
    {
        var config = new RestRequestConfig { Method = "GET" };

        var warnings = config.Validate().ToList();

        Assert.NotEmpty(warnings);
        Assert.Contains(warnings, w => w.Contains("requerido"));
    }

    [Fact]
    public void RestRequestConfig_InvalidMethod_HasWarning()
    {
        var config = new RestRequestConfig
        {
            Url = "https://api.example.com",
            Method = "INVALID"
        };

        var warnings = config.Validate().ToList();

        Assert.Contains(warnings, w => w.Contains("no soportado"));
    }
}
