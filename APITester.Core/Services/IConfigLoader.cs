namespace APITester.Core.Services;

public interface IConfigLoader<T> where T : class
{
    Task<List<T>> LoadAsync(string filePath);
}
