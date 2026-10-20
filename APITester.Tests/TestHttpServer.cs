using System.Net;
using System.Net.Sockets;
using System.Text;

namespace APITester.Tests;

/// <summary>
/// Servidor HTTP local minimo para los tests de integracion: evita depender de
/// Internet (y de la disponibilidad de una API publica) en el CI.
/// </summary>
internal sealed class TestHttpServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;

    public string BaseUrl { get; }
    public int StatusCode { get; set; } = 200;
    public string Body { get; set; } = """{"ok":true}""";
    public string ContentType { get; set; } = "application/json";
    public string? LastAuthorizationHeader { get; private set; }

    public TestHttpServer()
    {
        BaseUrl = $"http://127.0.0.1:{GetFreePort()}/";
        _listener.Prefixes.Add(BaseUrl);
        _listener.Start();
        _loop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    /// <summary>URL absoluta del servidor para el path indicado.</summary>
    public string Url(string path = "") => $"{BaseUrl}{path.TrimStart('/')}";

    private static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested || !_listener.IsListening)
            {
                return;
            }

            LastAuthorizationHeader = context.Request.Headers["Authorization"];

            var bytes = Encoding.UTF8.GetBytes(Body);
            context.Response.StatusCode = StatusCode;
            context.Response.ContentType = ContentType;
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            context.Response.Close();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _listener.Close();

        try
        {
            _loop.GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // El bucle termina con excepcion al cerrar el listener: es esperado.
        }

        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
