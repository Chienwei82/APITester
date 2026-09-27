using APITester.Rest;
using Spectre.Console;
using Spectre.Console.Testing;

namespace APITester.Tests;

/// <summary>
/// Pruebas del asistente paso a paso: se emula el teclado con TestConsole y se
/// comprueba el <c>RestRequestConfig</c> que devuelve, o null si el usuario
/// rechaza la confirmacion final.
/// </summary>
public class RequestWizardTests
{
    private static TestConsole NewConsole()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        return console;
    }

    private static void Select(TestConsole console, int index)
    {
        for (var i = 0; i < index; i++)
        {
            console.Input.PushKey(ConsoleKey.DownArrow);
        }

        console.Input.PushKey(ConsoleKey.Enter);
    }

    [Fact]
    public void Build_GetRequest_ReturnsAnswers()
    {
        var console = NewConsole();
        console.Input.PushTextWithEnter("prueba");              // Nombre
        Select(console, 0);                                      // Metodo: GET
        console.Input.PushTextWithEnter("https://api.dev/users");// URL
        console.Input.PushTextWithEnter("X-Test: 1");            // Header
        console.Input.PushTextWithEnter("");                     // Fin de headers
        console.Input.PushTextWithEnter("a=b");                  // Query
        console.Input.PushTextWithEnter("");                     // Fin de query
        Select(console, 0);                                      // Timeout: 30
        console.Input.PushTextWithEnter("2");                    // Reintentos
        console.Input.PushTextWithEnter("");                     // Sin salida
        console.Input.PushTextWithEnter("n");                    // Sin avanzado
        console.Input.PushTextWithEnter("y");                    // Ejecutar

        var config = new RequestWizard(console).Build();

        Assert.NotNull(config);
        Assert.Equal("prueba", config!.Name);
        Assert.Equal("GET", config.Method);
        Assert.Equal("https://api.dev/users", config.Url);
        Assert.Equal("1", config.Headers!["X-Test"]);
        Assert.Equal("b", config.Query!["a"]);
        Assert.Equal(30, config.TimeoutInSeconds);
        Assert.Equal(2, config.Retries);
        Assert.Null(config.Output);
        Assert.Null(config.Cert);
        Assert.Null(config.Body);
    }

    [Fact]
    public void Build_InvalidUrl_Reprompts()
    {
        var console = NewConsole();
        console.Input.PushTextWithEnter("");
        Select(console, 0);
        console.Input.PushTextWithEnter("no-es-url");
        console.Input.PushTextWithEnter("https://ok.dev");
        console.Input.PushTextWithEnter("");
        console.Input.PushTextWithEnter("");
        Select(console, 0);
        Select(console, 0);
        console.Input.PushTextWithEnter("");
        console.Input.PushTextWithEnter("n");
        console.Input.PushTextWithEnter("y");

        var config = new RequestWizard(console).Build();

        Assert.Equal("https://ok.dev", config!.Url);
        Assert.Contains("URL valida", console.Output.ToString());
    }

    [Fact]
    public void Build_InvalidTimeout_Reprompts()
    {
        var console = NewConsole();
        console.Input.PushTextWithEnter("");
        Select(console, 0);
        console.Input.PushTextWithEnter("https://api.dev");
        console.Input.PushTextWithEnter("");
        console.Input.PushTextWithEnter("");
        console.Input.PushTextWithEnter("0");                    // Timeout invalido
        console.Input.PushTextWithEnter("45");                   // Timeout valido
        Select(console, 0);                                      // Reintentos
        console.Input.PushTextWithEnter("");
        console.Input.PushTextWithEnter("n");
        console.Input.PushTextWithEnter("y");

        var config = new RequestWizard(console).Build();

        Assert.Equal(45, config!.TimeoutInSeconds);
    }

    [Fact]
    public void Build_PostRequest_ReadsBodyAndAdvancedOptions()
    {
        var console = NewConsole();
        console.Input.PushTextWithEnter("");
        Select(console, 1);                                      // Metodo: POST
        console.Input.PushTextWithEnter("https://api.dev/items");
        console.Input.PushTextWithEnter("");
        console.Input.PushTextWithEnter("");
        console.Input.PushTextWithEnter("{\"a\":1}");             // Body
        console.Input.PushTextWithEnter("");                     // Fin de body
        Select(console, 0);                                      // Timeout
        Select(console, 0);                                      // Reintentos
        console.Input.PushTextWithEnter("");
        console.Input.PushTextWithEnter("y");                    // Avanzado
        Select(console, 0);                                      // Delay: 1000
        console.Input.PushTextWithEnter("y");                    // Backoff
        Select(console, 0);                                      // MaxBody: 4194304
        console.Input.PushTextWithEnter("");                     // Sin certificado
        console.Input.PushTextWithEnter("y");                    // Ejecutar

        var config = new RequestWizard(console).Build();

        Assert.Equal("POST", config!.Method);
        Assert.Equal("{\"a\":1}", config.Body);
        Assert.Equal(1000, config.RetryDelayMilliseconds);
        Assert.True(config.UseExponentialBackoff);
        Assert.Equal(4 * 1024 * 1024, config.MaxBodyBytes);
        Assert.Null(config.Cert);
    }

    [Fact]
    public void Build_ForbiddenOrMalformedHeaders_AreRejected()
    {
        var console = NewConsole();
        console.Input.PushTextWithEnter("");
        Select(console, 0);
        console.Input.PushTextWithEnter("https://api.dev");
        console.Input.PushTextWithEnter("Host: evil");           // Prohibido
        console.Input.PushTextWithEnter("sin dos puntos");        // Formato invalido
        console.Input.PushTextWithEnter("X-Ok: si");             // Valido
        console.Input.PushTextWithEnter("");
        console.Input.PushTextWithEnter("");
        Select(console, 0);
        Select(console, 0);
        console.Input.PushTextWithEnter("");
        console.Input.PushTextWithEnter("n");
        console.Input.PushTextWithEnter("y");

        var config = new RequestWizard(console).Build();
        var output = console.Output.ToString();

        Assert.Equal("si", config!.Headers!["X-Ok"]);
        Assert.DoesNotContain("Host", config.Headers.Keys);
        Assert.Contains("no esta permitido por seguridad", output);
        Assert.Contains("Formato invalido", output);
    }

    [Fact]
    public void Build_MalformedQuery_ShowsError()
    {
        var console = NewConsole();
        console.Input.PushTextWithEnter("");
        Select(console, 0);
        console.Input.PushTextWithEnter("https://api.dev");
        console.Input.PushTextWithEnter("");
        console.Input.PushTextWithEnter("nada");                 // Sin '='
        console.Input.PushTextWithEnter("");
        Select(console, 0);
        Select(console, 0);
        console.Input.PushTextWithEnter("");
        console.Input.PushTextWithEnter("n");
        console.Input.PushTextWithEnter("y");

        var config = new RequestWizard(console).Build();

        Assert.Null(config!.Query);
        Assert.Contains("Formato invalido", console.Output.ToString());
    }

    [Fact]
    public void Build_OutputWithAppend_KeepsBoth()
    {
        var console = NewConsole();
        console.Input.PushTextWithEnter("");
        Select(console, 0);
        console.Input.PushTextWithEnter("https://api.dev");
        console.Input.PushTextWithEnter("");
        console.Input.PushTextWithEnter("");
        Select(console, 0);
        Select(console, 0);
        console.Input.PushTextWithEnter("out.json");
        console.Input.PushTextWithEnter("y");                    // Anadir
        console.Input.PushTextWithEnter("n");
        console.Input.PushTextWithEnter("y");

        var config = new RequestWizard(console).Build();

        Assert.Equal("out.json", config!.Output);
        Assert.True(config.AppendOutput);
    }

    [Fact]
    public void Build_DeclinedFinalConfirmation_ReturnsNull()
    {
        var console = NewConsole();
        console.Input.PushTextWithEnter("");
        Select(console, 0);
        console.Input.PushTextWithEnter("https://api.dev");
        console.Input.PushTextWithEnter("");
        console.Input.PushTextWithEnter("");
        Select(console, 0);
        Select(console, 0);
        console.Input.PushTextWithEnter("");
        console.Input.PushTextWithEnter("n");
        console.Input.PushTextWithEnter("n");

        var config = new RequestWizard(console).Build();

        Assert.Null(config);
    }

    [Fact]
    public void Build_AdvancedWithCertificate_SetsCertAndWarnsIfMissing()
    {
        var certPath = Path.GetTempFileName();
        try
        {
            var console = NewConsole();
            console.Input.PushTextWithEnter("");
            Select(console, 0);
            console.Input.PushTextWithEnter("https://api.dev");
            console.Input.PushTextWithEnter("");
            console.Input.PushTextWithEnter("");
            Select(console, 0);
            Select(console, 0);
            console.Input.PushTextWithEnter("");
            console.Input.PushTextWithEnter("y");                 // Avanzado
            Select(console, 0);                                   // Delay
            console.Input.PushTextWithEnter("n");                 // Backoff
            Select(console, 0);                                   // MaxBody
            console.Input.PushTextWithEnter(Path.Combine(Path.GetTempPath(), "no-existe.pfx"));
            console.Input.PushTextWithEnter("secreto");           // Password
            console.Input.PushTextWithEnter("y");                 // Ejecutar

            var config = new RequestWizard(console).Build();

            Assert.Equal("secreto", config!.Cert!.Password);
            Assert.Contains("Certificado no encontrado", console.Output.ToString());
        }
        finally
        {
            File.Delete(certPath);
        }
    }
}
