using System.Security.Cryptography.X509Certificates;
using APITester.Core.Models;

namespace APITester.Core.Services;

public static class CertHandlerFactory
{
    public static HttpClientHandler? Create(CertConfig? certConfig)
    {
        if (certConfig?.Path is null) return null;

        var cert = string.IsNullOrEmpty(certConfig.Password)
            ? X509CertificateLoader.LoadCertificateFromFile(certConfig.Path)
            : X509CertificateLoader.LoadPkcs12FromFile(certConfig.Path, certConfig.Password);

        return new HttpClientHandler { ClientCertificates = { cert } };
    }
}
