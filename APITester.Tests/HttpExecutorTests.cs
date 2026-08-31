using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using APITester.Core.Models;
using APITester.Rest.Models;
using APITester.Rest.Services;

namespace APITester.Tests;

public class HttpExecutorTests
{
    /// <summary>
    /// Mock HttpMessageHandler that captures the last request and returns a canned response.
    /// </summary>
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }
        public HttpHeaders ResponseHeaders => _response.Headers;

        public MockHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content is not null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            LastRequest = request;
            _response.RequestMessage = request;
            return _response;
        }
    }

    private static (HttpExecutor Executor, MockHttpMessageHandler Handler) CreateExecutor(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string body = "{}",
        string contentType = "application/json")
    {
        var handler = new MockHttpMessageHandler(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType)
        });
        var client = new HttpClient(handler);
        return (new HttpExecutor(httpClient: client), handler);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulGet_ReturnsResponse()
    {
        var (executor, handler) = CreateExecutor(
            HttpStatusCode.OK,
            """{"id": 1, "name": "Test"}""");

        var config = new RestRequestConfig
        {
            Url = "https://api.example.com/items/1",
            Method = "GET"
        };

        var result = await executor.ExecuteAsync(config);

        Assert.Null(result.Error);
        Assert.NotNull(result.Response);
        Assert.Equal(200, result.Response.StatusCode);
        Assert.Equal("OK", result.Response.StatusText);
        Assert.NotNull(result.Response.Body);
        Assert.Equal(1, result.Response.Body["id"]?.GetValue<int>());
        Assert.Equal("Test", result.Response.Body["name"]?.GetValue<string>());
        Assert.NotNull(result.Response.BodyRaw);
        Assert.True(result.Response.TimeMs >= 0);
        Assert.True(result.Response.SizeBytes > 0);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Equal("https://api.example.com/items/1", handler.LastRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_PostWithBody_SendsCorrectRequest()
    {
        var (executor, handler) = CreateExecutor(HttpStatusCode.Created);

        var config = new RestRequestConfig
        {
            Url = "https://api.example.com/items",
            Method = "POST",
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer test-token"
            },
            Body = """{"name": "New Item", "price": 100}"""
        };

        var result = await executor.ExecuteAsync(config);

        Assert.Null(result.Error);
        Assert.NotNull(result.Response);
        Assert.Equal(201, result.Response.StatusCode);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("New Item", handler.LastRequestBody);
        Assert.Contains("100", handler.LastRequestBody);

        var authHeader = handler.LastRequest.Headers.Authorization;
        Assert.NotNull(authHeader);
        Assert.Equal("Bearer", authHeader!.Scheme);
        Assert.Equal("test-token", authHeader.Parameter);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyUrl_ReturnsError()
    {
        var (executor, _) = CreateExecutor();

        var config = new RestRequestConfig
        {
            Url = "",
            Method = "GET"
        };

        var result = await executor.ExecuteAsync(config);

        Assert.NotNull(result.Error);
        Assert.Equal("La URL es requerida", result.Error);
        Assert.Null(result.Response);
    }

    [Fact]
    public async Task ExecuteAsync_NullUrl_ReturnsError()
    {
        var (executor, _) = CreateExecutor();

        var config = new RestRequestConfig
        {
            Url = null,
            Method = "GET"
        };

        var result = await executor.ExecuteAsync(config);

        Assert.NotNull(result.Error);
        Assert.Equal("La URL es requerida", result.Error);
        Assert.Null(result.Response);
    }

    [Fact]
    public async Task ExecuteAsync_WithQueryParameters_AppendsToUrl()
    {
        var (executor, handler) = CreateExecutor();

        var config = new RestRequestConfig
        {
            Url = "https://api.example.com/search",
            Method = "GET",
            Query = new Dictionary<string, string>
            {
                ["q"] = "test",
                ["page"] = "2",
                ["limit"] = "10"
            }
        };

        await executor.ExecuteAsync(config);

        Assert.NotNull(handler.LastRequest);
        var uri = handler.LastRequest.RequestUri!.ToString();
        Assert.Contains("q=test", uri);
        Assert.Contains("page=2", uri);
        Assert.Contains("limit=10", uri);
    }

    [Fact]
    public async Task ExecuteAsync_ServerError_ReturnsErrorResponse()
    {
        var (executor, _) = CreateExecutor(
            HttpStatusCode.InternalServerError,
            """{"error": "Internal failure"}""");

        var config = new RestRequestConfig
        {
            Url = "https://api.example.com/fail",
            Method = "GET"
        };

        var result = await executor.ExecuteAsync(config);

        Assert.Null(result.Error); // Server errors are not exceptions, just status codes
        Assert.NotNull(result.Response);
        Assert.Equal(500, result.Response.StatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_RequestInfo_IsPopulated()
    {
        var (executor, _) = CreateExecutor();

        var config = new RestRequestConfig
        {
            Name = "My Request",
            Url = "https://api.example.com/data",
            Method = "DELETE"
        };

        var result = await executor.ExecuteAsync(config);

        Assert.NotNull(result.Request);
        Assert.Equal("My Request", result.Request.Name);
        Assert.Equal("https://api.example.com/data", result.Request.Url);
        Assert.Equal("DELETE", result.Request.Method);
    }

    [Fact]
    public async Task ExecuteAsync_ForbiddenHeader_ThrowsInvalidOperationException()
    {
        var (executor, _) = CreateExecutor();

        var config = new RestRequestConfig
        {
            Url = "https://api.example.com/data",
            Method = "GET",
            Headers = new Dictionary<string, string>
            {
                ["Content-Length"] = "100"
            }
        };

        var result = await executor.ExecuteAsync(config);

        Assert.NotNull(result.Error);
        Assert.Contains("no esta permitido", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_HeaderWithNewline_ThrowsInvalidOperationException()
    {
        var (executor, _) = CreateExecutor();

        var config = new RestRequestConfig
        {
            Url = "https://api.example.com/data",
            Method = "GET",
            Headers = new Dictionary<string, string>
            {
                ["X-Custom"] = "value\r\nInjected: malicious"
            }
        };

        var result = await executor.ExecuteAsync(config);

        Assert.NotNull(result.Error);
        Assert.Contains("caracteres invalidos", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ValidHeaders_AreSent()
    {
        var (executor, handler) = CreateExecutor();

        var config = new RestRequestConfig
        {
            Url = "https://api.example.com/data",
            Method = "GET",
            Headers = new Dictionary<string, string>
            {
                ["X-Api-Key"] = "secret123",
                ["Accept"] = "application/json"
            }
        };

        await executor.ExecuteAsync(config);

        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest.Headers.Contains("X-Api-Key"));
        Assert.True(handler.LastRequest.Headers.Contains("Accept"));
    }

    [Fact]
    public async Task ExecuteAsync_RedactsSensitiveHeaders_InOutputByDefault()
    {
        var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });
        handler.ResponseHeaders.Add("Set-Cookie", "session=abc123; HttpOnly");
        var client = new HttpClient(handler);
        var executor = new HttpExecutor(httpClient: client);

        var config = new RestRequestConfig
        {
            Url = "https://api.example.com/items",
            Method = "POST",
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer test-token",
                ["X-Api-Key"] = "key-123"
            },
            Body = "{}"
        };

        var result = await executor.ExecuteAsync(config);

        // La salida redacta las credenciales...
        Assert.NotNull(result.Request.RequestHeaders);
        Assert.Equal("***", result.Request.RequestHeaders["Authorization"]);
        Assert.Equal("key-123", result.Request.RequestHeaders["X-Api-Key"]);
        Assert.NotNull(result.Response);
        Assert.NotNull(result.Response.Headers);
        Assert.Equal("***", result.Response.Headers["Set-Cookie"]);

        // ...pero el request HTTP real viaja completo.
        var authHeader = handler.LastRequest!.Headers.Authorization;
        Assert.NotNull(authHeader);
        Assert.Equal("test-token", authHeader!.Parameter);
    }

    [Fact]
    public async Task ExecuteAsync_NoRedact_KeepsHeadersInOutput()
    {
        var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });
        var client = new HttpClient(handler);
        var executor = new HttpExecutor(httpClient: client, redactHeaders: false);

        var config = new RestRequestConfig
        {
            Url = "https://api.example.com/items",
            Method = "GET",
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer test-token"
            }
        };

        var result = await executor.ExecuteAsync(config);

        Assert.NotNull(result.Request.RequestHeaders);
        Assert.Equal("Bearer test-token", result.Request.RequestHeaders["Authorization"]);
    }

    [Fact]
    public async Task ExecuteAsync_LargeBody_IsTruncatedAtMaxBodyBytes()
    {
        var bigBody = new string('a', 2048);
        var (executor, _) = CreateExecutor(HttpStatusCode.OK, bigBody);

        var config = new RestRequestConfig
        {
            Url = "https://api.example.com/large",
            Method = "GET",
            MaxBodyBytes = 1024
        };

        var result = await executor.ExecuteAsync(config);

        Assert.Null(result.Error);
        Assert.NotNull(result.Response);
        Assert.NotNull(result.Response.BodyRaw);
        Assert.Equal(1024, result.Response.BodyRaw.Length);
        // El tamano reportado es el real de la respuesta, no el truncado.
        Assert.Equal(2048, result.Response.SizeBytes);
    }

    [Fact]
    public async Task ExecuteAsync_SmallBody_NotTruncated()
    {
        var (executor, _) = CreateExecutor(HttpStatusCode.OK, "{\"ok\": true}");

        var config = new RestRequestConfig
        {
            Url = "https://api.example.com/small",
            Method = "GET"
        };

        var result = await executor.ExecuteAsync(config);

        Assert.NotNull(result.Response);
        Assert.NotNull(result.Response.Body);
        Assert.NotNull(result.Response.BodyRaw);
        Assert.True(result.Response.BodyRaw.Length > 0);
    }

    [Fact]
    public async Task ExecuteAsync_Timeout_ReturnsError()
    {
        // Use a handler that never completes, so the timeout triggers
        var neverHandler = new HttpMessageHandlerStub(ct => Task.Delay(Timeout.InfiniteTimeSpan, ct));
        var client = new HttpClient(neverHandler);

        var executor = new HttpExecutor(httpClient: client);

        var config = new RestRequestConfig
        {
            Url = "https://api.example.com/slow",
            Method = "GET",
            TimeoutInSeconds = 1 // 1 second timeout
        };

        var sw = Stopwatch.StartNew();
        var result = await executor.ExecuteAsync(config);
        sw.Stop();

        // Should fail with an error (timeout) and complete within ~2 seconds
        Assert.NotNull(result.Error);
        Assert.Null(result.Response);
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"Timeout test took too long: {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Stub HttpMessageHandler that waits using a user-provided function
    /// that receives the CancellationToken.
    /// </summary>
    private sealed class HttpMessageHandlerStub : HttpMessageHandler
    {
        private readonly Func<CancellationToken, Task> _waitAsync;

        public HttpMessageHandlerStub(Func<CancellationToken, Task> waitAsync)
        {
            _waitAsync = waitAsync;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await _waitAsync(cancellationToken);
            throw new InvalidOperationException("Should not reach here");
        }
    }
}
