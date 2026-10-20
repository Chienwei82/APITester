using System.Net.Http.Headers;

namespace APITester.Core.Services;

public static class HeaderCollector
{
    /// <summary>
    /// Aplana los headers de la respuesta en un unico diccionario; los valores
    /// repetidos de un mismo header se unen con ", ".
    /// </summary>
    public static Dictionary<string, string> CollectFrom(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Add(headers, response.Headers);
        Add(headers, response.Content.Headers);
        return headers;

        static void Add(Dictionary<string, string> target, HttpHeaders source)
        {
            foreach (var (name, values) in source)
            {
                var joined = string.Join(", ", values);
                target[name] = target.TryGetValue(name, out var existing)
                    ? $"{existing}, {joined}"
                    : joined;
            }
        }
    }
}
