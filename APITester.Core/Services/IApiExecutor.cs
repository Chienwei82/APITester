using APITester.Core.Models;

namespace APITester.Core.Services;

public interface IApiExecutor<T> where T : class
{
    Task<ApiResponse> ExecuteAsync(T config, CancellationToken cancellationToken = default);
}
