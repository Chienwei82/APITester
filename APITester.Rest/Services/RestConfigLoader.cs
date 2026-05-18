using APITester.Core.Services;
using APITester.Rest.Models;

namespace APITester.Rest.Services;

public static class RestConfigLoader
{
    private static readonly IConfigLoader<RestRequestConfig> Loader =
        new GenericConfigLoader<RestRequestConfig>(
            cfg => cfg.Url is not null,
            "url");

    public static Task<List<RestRequestConfig>> LoadAsync(string filePath) =>
        Loader.LoadAsync(filePath);
}
