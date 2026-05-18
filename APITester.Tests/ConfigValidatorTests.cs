using APITester.Core.Models;
using APITester.Core.Services;

namespace APITester.Tests;

public class ConfigValidatorTests
{
    [Theory]
    [InlineData(30, null)]
    [InlineData(1, null)]
    [InlineData(300, null)]
    [InlineData(0, "Timeout debe ser mayor a 0 segundos")]
    [InlineData(-1, "Timeout debe ser mayor a 0 segundos")]
    [InlineData(301, "Timeout no puede exceder 300 segundos")]
    public void ValidateTimeout_ValidAndInvalid(int timeout, string? expectedError)
    {
        var result = ConfigValidator.ValidateTimeout(timeout);
        Assert.Equal(expectedError, result);
    }

    [Theory]
    [InlineData("https://api.example.com", null)]
    [InlineData("http://localhost:8080", null)]
    [InlineData("", "Url es requerido")]
    [InlineData("   ", "Url es requerido")]
    [InlineData(null, "Url es requerido")]
    [InlineData("not-a-url", "Url no es una URL valida")]
    [InlineData("ftp://files.example.com", "Url debe usar http o https")]
    public void ValidateUrl_ValidAndInvalid(string? url, string? expectedError)
    {
        var result = ConfigValidator.ValidateUrl(url);
        Assert.Equal(expectedError, result);
    }

    [Fact]
    public void ValidateCert_NullCert_ReturnsNull()
    {
        var result = ConfigValidator.ValidateCert(null);
        Assert.Null(result);
    }

    [Fact]
    public void ValidateCert_MissingPath_ReturnsError()
    {
        var cert = new CertConfig { Path = "", Password = "pass" };
        var result = ConfigValidator.ValidateCert(cert);
        Assert.Equal("La ruta del certificado no puede estar vacia", result);
    }

    [Fact]
    public void ValidateCert_NonExistentFile_ReturnsError()
    {
        var cert = new CertConfig { Path = "/nonexistent/cert.pfx" };
        var result = ConfigValidator.ValidateCert(cert);
        Assert.Contains("Certificado no encontrado", result);
    }
}
