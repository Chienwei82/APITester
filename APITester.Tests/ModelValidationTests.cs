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

    [Fact]
    public void RestRequestConfig_BodyWithGetMethod_HasWarning()
    {
        var config = new RestRequestConfig
        {
            Url = "https://api.example.com",
            Method = "GET",
            Body = "{\"key\":\"value\"}"
        };

        var warnings = config.Validate().ToList();

        Assert.Contains(warnings, w => w.Contains("no soporta body"));
    }

    [Fact]
    public void RestRequestConfig_BodyWithPostMethod_NoWarning()
    {
        var config = new RestRequestConfig
        {
            Url = "https://api.example.com",
            Method = "POST",
            Body = "{\"key\":\"value\"}"
        };

        var warnings = config.Validate().ToList();

        Assert.DoesNotContain(warnings, w => w.Contains("no soporta body"));
    }

    [Fact]
    public void ApplyDefaults_BaseUrl_ResolvesRelativeUrl()
    {
        var config = new RestRequestConfig
        {
            Url = "/api/v1/users",
            Method = "GET"
        };

        config.ApplyDefaults(new RestConfigDefaults { BaseUrl = "https://example.com" });

        Assert.Equal("https://example.com/api/v1/users", config.Url);
    }

    [Fact]
    public void ApplyDefaults_BaseUrl_SkipsAbsoluteUrl()
    {
        var config = new RestRequestConfig
        {
            Url = "https://other.com/api",
            Method = "GET"
        };

        config.ApplyDefaults(new RestConfigDefaults { BaseUrl = "https://example.com" });

        Assert.Equal("https://other.com/api", config.Url);
    }

    [Fact]
    public void ApplyDefaults_NullDefaults_DoesNothing()
    {
        var config = new RestRequestConfig
        {
            Url = "/api/test",
            Method = "GET"
        };

        config.ApplyDefaults(null);

        Assert.Equal("/api/test", config.Url);
    }

    [Fact]
    public void ApplyDefaults_DefaultHeaders_AppliedWhenNull()
    {
        var config = new RestRequestConfig
        {
            Url = "https://example.com",
            Method = "GET"
        };

        config.ApplyDefaults(new RestConfigDefaults
        {
            Headers = new Dictionary<string, string> { { "Authorization", "Bearer token" } }
        });

        Assert.NotNull(config.Headers);
        Assert.Equal("Bearer token", config.Headers["Authorization"]);
    }

    [Fact]
    public void ApplyDefaults_DefaultHeaders_DoesNotOverrideExisting()
    {
        var config = new RestRequestConfig
        {
            Url = "https://example.com",
            Method = "GET",
            Headers = new Dictionary<string, string> { { "X-Custom", "value" } }
        };

        config.ApplyDefaults(new RestConfigDefaults
        {
            Headers = new Dictionary<string, string> { { "Authorization", "Bearer token" } }
        });

        Assert.Single(config.Headers);
        Assert.Equal("value", config.Headers["X-Custom"]);
    }

    [Fact]
    public void ApplyDefaults_Timeout_AppliedOnlyWhenExplicit()
    {
        var config = new RestRequestConfig
        {
            Url = "https://example.com",
            Method = "GET",
            TimeoutInSeconds = 60
        };

        config.ApplyDefaults(new RestConfigDefaults { TimeoutInSeconds = 10 });

        Assert.Equal(60, config.EffectiveTimeoutInSeconds);
    }

    [Fact]
    public void ApplyDefaults_Timeout_AppliedWhenNotSpecified()
    {
        var config = new RestRequestConfig
        {
            Url = "https://example.com",
            Method = "GET"
        };

        config.ApplyDefaults(new RestConfigDefaults { TimeoutInSeconds = 10 });

        Assert.Equal(10, config.EffectiveTimeoutInSeconds);
    }

    [Fact]
    public void ApplyDefaults_Timeout_NotOverridden_WhenExplicitlySetToClassDefault()
    {
        // Un valor definido explicitamente como 30 no debe ser sobrescrito
        // por un default de configuracion: el campo es ahora anulable.
        var config = new RestRequestConfig
        {
            Url = "https://example.com",
            Method = "GET",
            TimeoutInSeconds = 30
        };

        config.ApplyDefaults(new RestConfigDefaults { TimeoutInSeconds = 10 });

        Assert.Equal(30, config.EffectiveTimeoutInSeconds);
    }

    [Fact]
    public void ApplyDefaults_RetrySettings_AppliedWhenNull()
    {
        var config = new RestRequestConfig { Url = "https://example.com", Method = "GET" };

        config.ApplyDefaults(new RestConfigDefaults
        {
            Retries = 3,
            RetryDelayMilliseconds = 250,
            UseExponentialBackoff = true,
            RetryOnStatusCodes = [429, 503],
            MaxBodyBytes = 2048
        });

        Assert.Equal(3, config.EffectiveRetries);
        Assert.Equal(250, config.EffectiveRetryDelayMilliseconds);
        Assert.True(config.EffectiveUseExponentialBackoff);
        Assert.Equal([429, 503], config.RetryOnStatusCodes);
        Assert.Equal(2048, config.EffectiveMaxBodyBytes);
    }

    [Fact]
    public void ApplyDefaults_ExplicitRetrySettings_WinOverDefaults()
    {
        var config = new RestRequestConfig
        {
            Url = "https://example.com",
            Method = "GET",
            Retries = 1,
            UseExponentialBackoff = false,
            MaxBodyBytes = 512
        };

        config.ApplyDefaults(new RestConfigDefaults
        {
            Retries = 5,
            UseExponentialBackoff = true,
            MaxBodyBytes = 4096
        });

        Assert.Equal(1, config.EffectiveRetries);
        Assert.False(config.EffectiveUseExponentialBackoff);
        Assert.Equal(512, config.EffectiveMaxBodyBytes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Validate_RetriesOutOfRange_Warns(int retries)
    {
        var config = new RestRequestConfig
        {
            Url = "https://example.com",
            Method = "GET",
            Retries = retries
        };

        Assert.Contains(config.Validate(), w => w.Contains("Retries debe estar entre"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(60001)]
    public void Validate_RetryDelayOutOfRange_Warns(int delayMs)
    {
        var config = new RestRequestConfig
        {
            Url = "https://example.com",
            Method = "GET",
            RetryDelayMilliseconds = delayMs
        };

        Assert.Contains(config.Validate(), w => w.Contains("RetryDelayMs debe estar entre"));
    }

    [Fact]
    public void Validate_NegativeMaxBodyBytes_Warns()
    {
        var config = new RestRequestConfig
        {
            Url = "https://example.com",
            Method = "GET",
            MaxBodyBytes = -1
        };

        Assert.Contains(config.Validate(), w => w.Contains("MaxBodyBytes no puede ser negativo"));
    }
}
