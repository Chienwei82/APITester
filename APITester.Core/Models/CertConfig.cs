namespace APITester.Core.Models;

/// <summary>
/// Configuracion de certificado cliente TLS, compartido.
/// </summary>
public class CertConfig
{
    public string? Path { get; set; }
    public string? Password { get; set; }
}
