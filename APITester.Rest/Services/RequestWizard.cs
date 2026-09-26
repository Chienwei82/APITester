using System.Globalization;
using APITester.Core.Models;
using APITester.Core.Services;
using APITester.Rest.Models;
using Spectre.Console;

namespace APITester.Rest;

/// <summary>
/// Asistente paso a paso: pide todos los parametros de un request por pantalla,
/// valida cada respuesta y devuelve el <see cref="RestRequestConfig"/> listo para
/// ejecutar (o <c>null</c> si el usuario rechaza la confirmacion final).
/// </summary>
public sealed class RequestWizard(IAnsiConsole console)
{
    private int _step;

    public RestRequestConfig? Build(CancellationToken cancellationToken = default)
    {
        _step = 0;
        var config = new RestRequestConfig();

        Section("Nombre");
        config.Name = NullIfEmpty(Tui.AskTextOptional(console, "Nombre (Enter para omitir)", cancellationToken));

        Section("Metodo HTTP");
        config.Method = Tui.Choose(console, "Metodo HTTP", static method => method, HttpMethods.All, cancellationToken);

        Section("URL");
        config.Url = Tui.AskText(console, "URL", cancellationToken, url => ConfigValidator.ValidateUrl(url));

        config.Headers = NonEmpty(ReadHeaders(cancellationToken));
        config.Query = NonEmpty(ReadQuery(cancellationToken));

        if (HttpMethods.AllowsBody(config.Method))
        {
            config.Body = ReadBody(cancellationToken);
        }

        Section("Timeout y reintentos");
        config.TimeoutInSeconds = Tui.AskInt(console, "Timeout en segundos (Enter = 30)", 30,
            ConfigValidator.ValidateTimeout, cancellationToken);
        config.Retries = Tui.AskInt(console, "Reintentos (Enter = 0)", 0,
            retries => retries is < 0 or > 10 ? "Reintentos debe estar entre 0 y 10" : null,
            cancellationToken);

        Section("Salida");
        var output = NullIfEmpty(Tui.AskTextOptional(console, "Archivo de salida (Enter = no guardar)", cancellationToken));
        if (output is not null)
        {
            config.Output = output;
            config.AppendOutput = Tui.Confirm(console, "Anadir al archivo en vez de sobrescribirlo", false, cancellationToken);
        }

        if (Tui.Confirm(console, "Configuracion avanzada (delay, backoff, cuerpo maximo, certificado)", false, cancellationToken))
        {
            ReadAdvanced(config, cancellationToken);
        }

        ShowSummary(config);
        return Tui.Confirm(console, "Ejecutar el request", true, cancellationToken) ? config : null;
    }

    private Dictionary<string, string> ReadHeaders(CancellationToken cancellationToken)
    {
        Section("Headers");
        console.MarkupLine("[grey]Uno por linea como 'Nombre: Valor'; Enter vacio termina.[/]");

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var line = Tui.AskTextOptional(console, "  Header", cancellationToken).Trim();
            if (line.Length == 0)
            {
                return headers;
            }

            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                Tui.Error(console, "Formato invalido: usa 'Nombre: Valor'.");
                continue;
            }

            var name = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (name.Length == 0 || name.Any(char.IsWhiteSpace))
            {
                Tui.Error(console, $"Nombre de header invalido: '{name}'.");
                continue;
            }

            var error = HeaderRules.Validate(name, value);
            if (error is not null)
            {
                Tui.Error(console, error);
                continue;
            }

            headers[name] = value;
        }
    }

    private Dictionary<string, string> ReadQuery(CancellationToken cancellationToken)
    {
        Section("Query");
        console.MarkupLine("[grey]Uno por linea como 'clave=valor'; Enter vacio termina.[/]");

        var query = new Dictionary<string, string>();
        while (true)
        {
            var line = Tui.AskTextOptional(console, "  Parametro", cancellationToken);
            if (line.Length == 0)
            {
                return query;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                Tui.Error(console, "Formato invalido: usa 'clave=valor'.");
                continue;
            }

            query[line[..separator].Trim()] = line[(separator + 1)..];
        }
    }

    private string? ReadBody(CancellationToken cancellationToken)
    {
        Section("Body");
        console.MarkupLine("[grey]Varias lineas; una linea vacia termina (Enter al principio = sin body).[/]");

        var lines = new List<string>();
        while (true)
        {
            var line = Tui.AskTextOptional(console, "  >", cancellationToken);
            if (line.Length == 0)
            {
                break;
            }

            lines.Add(line);
        }

        return lines.Count > 0 ? string.Join('\n', lines) : null;
    }

    private void ReadAdvanced(RestRequestConfig config, CancellationToken cancellationToken)
    {
        Section("Configuracion avanzada");

        config.RetryDelayMilliseconds = Tui.AskInt(console, "Delay entre reintentos en ms (Enter = 1000)", 1000,
            delay => delay is < 0 or > 60000 ? "El delay debe estar entre 0 y 60000 ms" : null, cancellationToken);

        config.UseExponentialBackoff = Tui.Confirm(console, "Backoff exponencial",
            config.UseExponentialBackoff, cancellationToken);

        config.MaxBodyBytes = Tui.AskInt(console, "Limite de la respuesta en bytes (Enter = 4194304)",
            4 * 1024 * 1024, max => max <= 0 ? "El limite debe ser mayor a 0" : null, cancellationToken);

        var certPath = NullIfEmpty(Tui.AskTextOptional(console, "Ruta del certificado cliente (Enter = ninguno)", cancellationToken));
        if (certPath is null)
        {
            return;
        }

        config.Cert = new CertConfig { Path = certPath };
        var certPassword = Tui.AskSecret(console, "Password del certificado (Enter = ninguno)", cancellationToken);
        config.Cert.Password = NullIfEmpty(certPassword);

        var error = ConfigValidator.ValidateCert(config.Cert);
        if (error is not null)
        {
            Tui.Warn(console, $"{error} (se validara de nuevo al ejecutar)");
        }
    }

    private void ShowSummary(RestRequestConfig config)
    {
        Section("Resumen");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .HideHeaders()
            .AddColumn("Campo")
            .AddColumn("Valor");

        table.AddRow("[bold]Metodo[/]", MethodChip(config.Method));
        table.AddRow("[bold]URL[/]", Tui.Esc(config.Url ?? string.Empty));
        table.AddRow("[bold]Nombre[/]", Tui.Esc(config.Name ?? "-"));
        table.AddRow("[bold]Headers[/]", Summarize(config.Headers));
        table.AddRow("[bold]Query[/]", Summarize(config.Query));
        table.AddRow("[bold]Body[/]", config.Body is null ? "-" : $"{config.Body.Length} caracteres");
        table.AddRow("[bold]Timeout[/]", $"{config.TimeoutInSeconds}s");
        table.AddRow("[bold]Reintentos[/]", config.Retries.GetValueOrDefault().ToString(CultureInfo.InvariantCulture));
        table.AddRow("[bold]Salida[/]", config.Output is null ? "(no guardar)" : Tui.Esc(config.Output));
        table.AddRow("[bold]Certificado[/]", config.Cert is null ? "-" : Tui.Esc(config.Cert.Path ?? "-"));

        console.Write(table);
    }

    private void Section(string title) => Tui.Section(console, $"Paso {++_step} — {title}");

    private static string Summarize(Dictionary<string, string>? values) =>
        values is null || values.Count == 0 ? "-" : string.Join(", ", values.Keys);

    private static string MethodChip(string method) => method switch
    {
        "GET" => "[bold green]GET[/]",
        "POST" => "[bold yellow]POST[/]",
        "PUT" => "[bold blue]PUT[/]",
        "PATCH" => "[bold magenta]PATCH[/]",
        "DELETE" => "[bold red]DELETE[/]",
        _ => $"[bold]{Tui.Esc(method)}[/]",
    };

    private static Dictionary<string, string>? NonEmpty(Dictionary<string, string> values) =>
        values.Count > 0 ? values : null;

    private static string? NullIfEmpty(string value) => value.Trim().Length == 0 ? null : value.Trim();
}
