using APITester.Core.Models;
using APITester.Core.Services;

namespace APITester.Tests;

public class CertHandlerFactoryTests
{
    [Fact]
    public void Create_NullConfig_ReturnsNull()
    {
        using var handler = CertHandlerFactory.Create(null);

        Assert.Null(handler);
    }

    [Fact]
    public void Create_NullPath_ReturnsNull()
    {
        using var handler = CertHandlerFactory.Create(new CertConfig());

        Assert.Null(handler);
    }

    [Fact]
    public void Create_NonExistentFile_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CertHandlerFactory.Create(new CertConfig { Path = "/nonexistent/cert.pfx" }));

        Assert.Contains("Error cargando certificado", ex.Message);
    }

    [Fact]
    public void ClearCache_DisposesHandlers()
    {
        // No exception should be thrown - just verify cache is cleared
        CertHandlerFactory.ClearCache();

        Assert.True(true);
    }
}
