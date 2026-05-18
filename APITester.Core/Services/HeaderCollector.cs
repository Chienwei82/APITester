namespace APITester.Core.Services;

public static class HeaderCollector
{
    public static Dictionary<string, string> CollectFrom(HttpResponseMessage response)
    {
        var all = response.Headers
            .SelectMany(h => h.Value.Select(v => (h.Key, v)))
            .Concat(response.Content.Headers
                .SelectMany(h => h.Value.Select(v => (h.Key, v))));

        return all
            .GroupBy(e => e.Key)
            .ToDictionary(
                g => g.Key,
                g => string.Join(", ", g.Select(e => e.v)));
    }
}
