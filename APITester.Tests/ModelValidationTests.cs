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
    public void RestRequestConfig_RetriesOutOfRange_HasWarning()
    {
        var config = new RestRequestConfig
        {
            Url = "https://example.com",
            Retries = 50
        };

        var warnings = config.Validate().ToList();

        Assert.Contains(warnings, w => w.Contains("Retries"));
    }

    [Fact]
    public void RestRequestConfig_RetriesNegative_HasWarning()
    {
        var config = new RestRequestConfig
        {
            Url = "https://example.com",
            Retries = -1
        };

        var warnings = config.Validate().ToList();

        Assert.Contains(warnings, w => w.Contains("Retries"));
    }

    [Fact]
    public void RestRequestConfig_RetryDelayOutOfRange_HasWarning()
    {
        var config = new RestRequestConfig
        {
            Url = "https://example.com",
            RetryDelayMilliseconds = 70000
        };

        var warnings = config.Validate().ToList();

        Assert.Contains(warnings, w => w.Contains("RetryDelayMs"));
    }

    [Fact]
    public void RestRequestConfig_RetriesInRange_NoWarning()
    {
        var config = new RestRequestConfig
        {
            Url = "https://example.com",
            Retries = 10,
            RetryDelayMilliseconds = 60000
        };

        var warnings = config.Validate().ToList();

        Assert.DoesNotContain(warnings, w => w.Contains("Retries"));
        Assert.DoesNotContain(warnings, w => w.Contains("RetryDelayMs"));
    }

    [Fact]
    public void ApplyDefaults_RetrySettingsAndMaxBodyBytes_AppliedWhenNull()
    {
        var config = new RestRequestConfig
        {
            Url = "https://example.com",
            Method = "GET"
        };

        config.ApplyDefaults(new RestConfigDefaults
        {
            UseExponentialBackoff = true,
            RetryOnStatusCodes = [429, 503],
            MaxBodyBytes = 1024
        });

        Assert.True(config.EffectiveUseExponentialBackoff);
        Assert.NotNull(config.RetryOnStatusCodes);
        Assert.Equal([429, 503], config.RetryOnStatusCodes);
        Assert.Equal(1024, config.MaxBodyBytes);
    }

    [Fact]
    public void ApplyDefaults_RetrySettings_DoNotOverrideExisting()
    {
        var config = new RestRequestConfig
        {
            Url = "https://example.com",
            Method = "GET",
            UseExponentialBackoff = false,
            RetryOnStatusCodes = [500],
            MaxBodyBytes = 2048
        };

        config.ApplyDefaults(new RestConfigDefaults
        {
            UseExponentialBackoff = true,
            RetryOnStatusCodes = [429],
            MaxBodyBytes = 1024
        });

        Assert.False(config.EffectiveUseExponentialBackoff);
        Assert.Equal([500], config.RetryOnStatusCodes);
        Assert.Equal(2048, config.MaxBodyBytes);
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
}
