using Spectre.Console;

namespace APITester.Rest;

/// <summary>
/// Helpers de aspecto TUI (banner, secciones, estados en color y prompts)
/// compartidos por el menu interactivo y el asistente paso a paso.
/// </summary>
public static class Tui
{
    /// <summary>Escapa el texto para que se muestre literal dentro de markup.</summary>
    public static string Esc(string text) => Markup.Escape(text);

    public static void Banner(IAnsiConsole console)
    {
        console.Write(new Rule("[bold cyan]API Tester — modo interactivo[/]"));
        console.MarkupLine("[grey]Flechas para navegar; Enter para elegir. Ctrl+C cancela la ejecucion en curso o sale de la aplicacion.[/]");
        console.WriteLine();
    }

    public static void Section(IAnsiConsole console, string title) =>
        console.Write(new Rule($"[bold]{Esc(title)}[/]"));

    public static void Info(IAnsiConsole console, string message) =>
        console.MarkupLine($"[cyan]·[/] {Esc(message)}");

    public static void Success(IAnsiConsole console, string message) =>
        console.MarkupLine($"[green]✓[/] {Esc(message)}");

    public static void Warn(IAnsiConsole console, string message) =>
        console.MarkupLine($"[yellow]![/] {Esc(message)}");

    public static void Error(IAnsiConsole console, string message) =>
        console.MarkupLine($"[red]✗[/] {Esc(message)}");

    /// <summary>
    /// Selector con flechas. Las etiquetas (devueltas por <paramref name="label"/>)
    /// admiten markup; el elemento seleccionado se muestra en plano con el estilo
    /// de resaltado.
    /// </summary>
    public static T Choose<T>(
        IAnsiConsole console,
        string title,
        Func<T, string> label,
        IEnumerable<T> choices,
        CancellationToken cancellationToken)
        where T : notnull
    {
        cancellationToken.ThrowIfCancellationRequested();

        var prompt = new SelectionPrompt<T>
        {
            Title = $"[bold]{Esc(title)}[/]",
            PageSize = 10,
            HighlightStyle = new Style(Color.Cyan, Color.Black),
            MoreChoicesText = "[grey](mas opciones)[/]",
            Converter = choice => label(choice),
        };

        foreach (var choice in choices)
        {
            prompt.AddChoice(choice);
        }

        return prompt.Show(console);
    }

    /// <summary>Pide una linea no vacia; con <paramref name="validator"/> repite hasta que sea valida.</summary>
    public static string AskText(
        IAnsiConsole console,
        string label,
        CancellationToken cancellationToken,
        Func<string, string?>? validator = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var prompt = new TextPrompt<string>(label);
        if (validator is not null)
        {
            prompt = prompt.Validate(value =>
                validator(value) is { } error ? ValidationResult.Error(error) : ValidationResult.Success());
        }

        return prompt.Show(console);
    }

    /// <summary>Pide una linea que puede estar vacia (Enter = omitir).</summary>
    public static string AskTextOptional(IAnsiConsole console, string label, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new TextPrompt<string>(label).AllowEmpty().Show(console);
    }

    /// <summary>Pide un entero con valor por defecto (Enter lo usa) y validacion opcional.</summary>
    public static int AskInt(
        IAnsiConsole console,
        string label,
        int defaultValue,
        Func<int, string?>? validator,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var prompt = new TextPrompt<int>(label).DefaultValue(defaultValue);
        if (validator is not null)
        {
            prompt = prompt.Validate(value =>
                validator(value) is { } error ? ValidationResult.Error(error) : ValidationResult.Success());
        }

        return prompt.Show(console);
    }

    /// <summary>Pide una linea con la entrada oculta (password).</summary>
    public static string AskSecret(IAnsiConsole console, string label, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new TextPrompt<string>(label).Secret().AllowEmpty().Show(console);
    }

    /// <summary>Pregunta si/no con valor por defecto; Enter lo acepta.</summary>
    public static bool Confirm(
        IAnsiConsole console,
        string question,
        bool defaultValue,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ConfirmationPrompt(question)
        {
            DefaultValue = defaultValue,
            RequireEnter = true,
        }.Show(console);
    }
}
